using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using YtLight.Core.Abstractions;
using YtLight.Core.Models;
using YtLight.Core.Session;
using YtLight.InnerTube.Auth;
using YtLight.InnerTube.Parsing;

namespace YtLight.InnerTube;

public sealed class InnerTubeClient : IInnerTubeClient
{
    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly InnerTubeOptions _options;
    private readonly YoutubeMusicBootstrapper _bootstrapper;
    private readonly ILogger<InnerTubeClient> _logger;
    private readonly object _gate = new();
    private AuthSession? _session;

    public InnerTubeClient(
        HttpClient http,
        InnerTubeOptions options,
        YoutubeMusicBootstrapper bootstrapper,
        ILogger<InnerTubeClient> logger)
    {
        _http = http;
        _options = options;
        _bootstrapper = bootstrapper;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.AcceptEncoding.Clear();
        _http.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        _http.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
        _http.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
    }

    public YoutubeProfile? ActiveProfile { get; private set; }

    public bool IsAuthenticated
    {
        get
        {
            lock (_gate)
                return _session?.IsAuthenticated == true;
        }
    }

    public void SetSession(AuthSession? session)
    {
        lock (_gate)
        {
            _session = session;
            ActiveProfile = session?.ActiveProfile;
        }
    }

    public void SetActiveProfile(YoutubeProfile? profile)
    {
        lock (_gate)
        {
            ActiveProfile = profile;
            if (_session is not null)
                _session = _session.WithProfile(profile);
        }
    }

    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        AuthSession? session;
        lock (_gate) session = _session;
        await _bootstrapper.ApplyAsync(_options, session, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(_bootstrapper.LastVisitorData) && session is not null)
        {
            lock (_gate)
            {
                _session = (_session ?? session).WithVisitorData(_bootstrapper.LastVisitorData);
            }
        }
    }

    public async Task<IReadOnlyList<YoutubeProfile>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await PostAsync("account/accounts_list", new Dictionary<string, object?>
        {
            ["accountSupportedIcons"] = new[] { "ALL" }
        }, cancellationToken, requireAuth: true).ConfigureAwait(false);

        return AccountListParser.Parse(doc.RootElement);
    }

    public async Task<LibrarySnapshot> GetLibraryAsync(CancellationToken cancellationToken = default)
    {
        var playlistsTask = BrowseAsync("FEmusic_liked_playlists", cancellationToken, requireAuth: true);
        var libraryTask = BrowseAsync("FEmusic_library_landing", cancellationToken, requireAuth: true);
        var albumsTask = BrowseAsync("FEmusic_liked_albums", cancellationToken, requireAuth: true);
        await Task.WhenAll(playlistsTask, libraryTask, albumsTask).ConfigureAwait(false);

        using var playlistsDoc = await playlistsTask.ConfigureAwait(false);
        using var libraryDoc = await libraryTask.ConfigureAwait(false);
        using var albumsDoc = await albumsTask.ConfigureAwait(false);

        var fromPlaylists = MusicParsers.ParseLibrary(playlistsDoc.RootElement);
        var fromLibrary = MusicParsers.ParseLibrary(libraryDoc.RootElement);
        var fromAlbums = MusicParsers.ParseLibrary(albumsDoc.RootElement);

        var liked = new PlaylistSummary
        {
            PlaylistId = "LM",
            Title = "Beğenilen Müzikler",
            IsLikedSongs = true,
            IsOwn = true
        };

        var mergedPlaylists = new[] { liked }
            .Concat(fromPlaylists.Playlists)
            .Concat(fromLibrary.Playlists)
            .DistinctBy(p => p.PlaylistId)
            .ToList();

        return new LibrarySnapshot
        {
            Playlists = mergedPlaylists,
            Albums = fromAlbums.Albums.Concat(fromLibrary.Albums).DistinctBy(a => a.BrowseId).ToList(),
            Recents = fromLibrary.Recents
        };
    }

    public async Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(string playlistId, CancellationToken cancellationToken = default)
    {
        var browseId = playlistId.StartsWith("VL", StringComparison.Ordinal) || playlistId.StartsWith("FE", StringComparison.Ordinal)
            ? playlistId
            : "VL" + playlistId;

        var allTracks = new List<Track>();
        using var doc = await BrowseAsync(browseId, cancellationToken, requireAuth: false).ConfigureAwait(false);
        var initial = MusicParsers.ParsePlaylistTracks(doc.RootElement);
        allTracks.AddRange(initial);

        var continuationToken = MusicParsers.FindContinuationToken(doc.RootElement);
        var safetyLimit = 50; // Fetch up to 5,000 songs per playlist
        while (!string.IsNullOrWhiteSpace(continuationToken) && safetyLimit-- > 0 && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var contDoc = await PostAsync("browse", new Dictionary<string, object?>
                {
                    ["continuation"] = continuationToken
                }, cancellationToken, requireAuth: false).ConfigureAwait(false);

                var batch = MusicParsers.ParsePlaylistTracks(contDoc.RootElement);
                if (batch.Count == 0)
                    break;

                allTracks.AddRange(batch);
                continuationToken = MusicParsers.FindContinuationToken(contDoc.RootElement);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Playlist continuation fetch ended");
                break;
            }
        }

        return allTracks.DistinctBy(t => t.VideoId + (t.SetVideoId ?? "")).ToList();
    }

    public async Task<HomeSnapshot> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await BrowseAsync("FEmusic_home", cancellationToken, requireAuth: false).ConfigureAwait(false);
        return MusicParsers.ParseHome(doc.RootElement);
    }

    public async Task<HomeSnapshot> GetExploreAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await BrowseAsync("FEmusic_explore", cancellationToken, requireAuth: false).ConfigureAwait(false);
        return MusicParsers.ParseHome(doc.RootElement);
    }

    public async Task<SearchSnapshot> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        using var doc = await PostAsync("search", new Dictionary<string, object?>
        {
            ["query"] = query
        }, cancellationToken, requireAuth: false).ConfigureAwait(false);
        return MusicParsers.ParseSearch(query, doc.RootElement);
    }

    public async Task<PlayerPayload> GetPlayerAsync(string videoId, CancellationToken cancellationToken = default)
    {
        // 1. Web player (WEB_REMIX matches session cookies and returns direct audio streams)
        try
        {
            var playbackContext = new Dictionary<string, object?>
            {
                ["contentPlaybackContext"] = new Dictionary<string, object?>
                {
                    ["html5Preference"] = "HTML5_PREF_WANTS",
                    ["signatureTimestamp"] = _options.SignatureTimestamp
                }
            };

            using var doc = await PostAsync("player", new Dictionary<string, object?>
            {
                ["videoId"] = videoId,
                ["playbackContext"] = playbackContext,
                ["racyCheckOk"] = true,
                ["contentCheckOk"] = true
            }, cancellationToken, requireAuth: false).ConfigureAwait(false);

            var rawJson = doc.RootElement.GetRawText();
            Console.WriteLine($"[DEBUG PLAYER FULL RESP] {rawJson}");

            var web = MusicParsers.ParsePlayer(videoId, doc.RootElement);
            if (web.AudioStreams.Count > 0 && web.AudioStreams.Any(s => !string.IsNullOrEmpty(s.Url)))
            {
                return web;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WEB_REMIX player failed, falling back to Android");
        }

        // 2. Android Music player fallback
        try
        {
            var android = await GetAndroidPlayerAsync(videoId, cancellationToken).ConfigureAwait(false);
            if (android.AudioStreams.Count > 0 && android.AudioStreams.Any(s => !string.IsNullOrEmpty(s.Url)))
            {
                return android;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ANDROID_MUSIC player failed");
        }

        return new PlayerPayload { Track = new Track { VideoId = videoId, Title = "Bilinmeyen", Artists = "" }, AudioStreams = [] };
    }

    public async Task<PlayerPayload> GetAndroidPlayerAsync(string videoId, CancellationToken cancellationToken = default)
    {
        using var doc = await PostAsync("player", new Dictionary<string, object?>
        {
            ["videoId"] = videoId,
            ["racyCheckOk"] = true,
            ["contentCheckOk"] = true
        }, cancellationToken, ClientOverride.AndroidMusic(_options), requireAuth: false).ConfigureAwait(false);
        return MusicParsers.ParsePlayer(videoId, doc.RootElement);
    }

    public async Task SetLikeAsync(string videoId, LikeStatus status, CancellationToken cancellationToken = default)
    {
        var endpoint = status switch
        {
            LikeStatus.Like => "like/like",
            LikeStatus.Dislike => "like/dislike",
            _ => "like/removelike"
        };

        using var _ = await PostAsync(endpoint, new Dictionary<string, object?>
        {
            ["target"] = new Dictionary<string, object?> { ["videoId"] = videoId }
        }, cancellationToken, requireAuth: true).ConfigureAwait(false);
    }

    public async Task<string> CreatePlaylistAsync(string title, IEnumerable<string>? videoIds = null, CancellationToken cancellationToken = default)
    {
        using var doc = await PostAsync("playlist/create", new Dictionary<string, object?>
        {
            ["title"] = title,
            ["privacyStatus"] = "PRIVATE",
            ["videoIds"] = videoIds?.ToArray() ?? Array.Empty<string>()
        }, cancellationToken, requireAuth: true).ConfigureAwait(false);

        return doc.RootElement.TryGetProperty("playlistId", out var id)
            ? id.GetString() ?? ""
            : "";
    }

    public async Task AddToPlaylistAsync(string playlistId, string videoId, CancellationToken cancellationToken = default)
    {
        using var _ = await PostAsync("browse/edit_playlist", new Dictionary<string, object?>
        {
            ["playlistId"] = NormalizePlaylistId(playlistId),
            ["actions"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["action"] = "ACTION_ADD_VIDEO",
                    ["addedVideoId"] = videoId
                }
            }
        }, cancellationToken, requireAuth: true).ConfigureAwait(false);
    }

    public async Task RemoveFromPlaylistAsync(string playlistId, string videoId, string? setVideoId, CancellationToken cancellationToken = default)
    {
        var action = new Dictionary<string, object?>
        {
            ["action"] = "ACTION_REMOVE_VIDEO",
            ["removedVideoId"] = videoId
        };
        if (!string.IsNullOrWhiteSpace(setVideoId))
            action["setVideoId"] = setVideoId;

        using var _ = await PostAsync("browse/edit_playlist", new Dictionary<string, object?>
        {
            ["playlistId"] = NormalizePlaylistId(playlistId),
            ["actions"] = new object[] { action }
        }, cancellationToken, requireAuth: true).ConfigureAwait(false);
    }

    public async Task<JsonDocument> PostRawAsync(string endpoint, IReadOnlyDictionary<string, object?> payload, CancellationToken cancellationToken = default)
        => await PostAsync(endpoint, payload, cancellationToken, requireAuth: false).ConfigureAwait(false);

    private async Task<JsonDocument> BrowseAsync(string browseId, CancellationToken cancellationToken, bool requireAuth = false)
        => await PostAsync("browse", new Dictionary<string, object?> { ["browseId"] = browseId }, cancellationToken, requireAuth: requireAuth)
            .ConfigureAwait(false);

    private static string NormalizePlaylistId(string playlistId) =>
        playlistId.StartsWith("VL", StringComparison.Ordinal) ? playlistId[2..] : playlistId;

    private async Task<JsonDocument> PostAsync(
        string endpoint,
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken,
        ClientOverride? client = null,
        bool requireAuth = false)
    {
        AuthSession? session;
        YoutubeProfile? profile;
        lock (_gate)
        {
            session = _session;
            profile = ActiveProfile ?? _session?.ActiveProfile;
        }

        if (requireAuth && (session is null || !session.IsAuthenticated))
            throw new InnerTubeException(endpoint, 401, "Oturum çerezleri eksik. Lütfen önce Google ile giriş yapın.");

        client ??= ClientOverride.WebRemix(_options);
        var body = BuildBody(payload, session, profile, client);
        var json = JsonSerializer.Serialize(body, PayloadJson);
        var url = $"{client.ApiOrigin}/youtubei/v1/{endpoint}?prettyPrint=false&key={Uri.EscapeDataString(client.ApiKey)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        ApplyHeaders(request, session, profile, client);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("InnerTube {Endpoint} failed: {Status} {Body}", endpoint, (int)response.StatusCode, Truncate(raw));
            throw new InnerTubeException(endpoint, (int)response.StatusCode, $"InnerTube {endpoint} HTTP {(int)response.StatusCode}", raw);
        }

        return JsonDocument.Parse(raw);
    }

    private Dictionary<string, object?> BuildBody(
        IReadOnlyDictionary<string, object?> payload,
        AuthSession? session,
        YoutubeProfile? profile,
        ClientOverride clientOverride)
    {
        var client = new Dictionary<string, object?>
        {
            ["clientName"] = clientOverride.ClientName,
            ["clientVersion"] = clientOverride.ClientVersion,
            ["hl"] = _options.Hl,
            ["gl"] = _options.Gl,
            ["userAgent"] = clientOverride.UserAgent,
            ["visitorData"] = session?.VisitorData
        };
        foreach (var field in clientOverride.ExtraClientFields)
            client[field.Key] = field.Value;

        var context = new Dictionary<string, object?>
        {
            ["client"] = client,
            ["user"] = new Dictionary<string, object?> { ["lockedSafetyMode"] = false },
            ["request"] = new Dictionary<string, object?>
            {
                ["internalExperimentFlags"] = Array.Empty<object>(),
                ["useSsl"] = true
            }
        };

        var body = new Dictionary<string, object?>(payload, StringComparer.Ordinal)
        {
            ["context"] = context
        };
        return body;
    }

    private void ApplyHeaders(HttpRequestMessage request, AuthSession? session, YoutubeProfile? profile, ClientOverride clientOverride)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", clientOverride.UserAgent);
        request.Headers.TryAddWithoutValidation("Origin", clientOverride.ApiOrigin);
        request.Headers.TryAddWithoutValidation("Referer", clientOverride.ApiOrigin + "/");
        request.Headers.TryAddWithoutValidation("X-Youtube-Client-Name", clientOverride.ClientNameId.ToString());
        request.Headers.TryAddWithoutValidation("X-Youtube-Client-Version", clientOverride.ClientVersion);

        if (session is not null && session.IsAuthenticated)
        {
            request.Headers.TryAddWithoutValidation("X-Goog-AuthUser", "0");
            request.Headers.TryAddWithoutValidation("Cookie", session.ToCookieHeader());

            try
            {
                var auth = SapisidHash.BuildAuthorizationHeader(session.Sapisid, session.Secure1Papisid, session.Secure3Papisid, InnerTubeOptions.DefaultOrigin);
                request.Headers.TryAddWithoutValidation("Authorization", auth);
            }
            catch
            {
                // anonymous fallback
            }

            if (!string.IsNullOrWhiteSpace(session.VisitorData))
                request.Headers.TryAddWithoutValidation("X-Goog-Visitor-Id", session.VisitorData);

            if (profile is not null && profile.IsBrandAccount && !string.IsNullOrWhiteSpace(profile.PageId))
            {
                request.Headers.TryAddWithoutValidation("X-Goog-PageId", profile.PageId);
            }
        }
    }

    private static string Truncate(string raw) => raw.Length <= 400 ? raw : raw[..400];
}

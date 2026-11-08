using System.Text.Json;
using System.Text.RegularExpressions;
using YtLight.Core.Models;
using YtLight.InnerTube.Json;

namespace YtLight.InnerTube.Parsing;

public static class MusicParsers
{
    public static LibrarySnapshot ParseLibrary(JsonElement root)
    {
        var playlists = new List<PlaylistSummary>();
        var albums = new List<AlbumSummary>();
        var recents = new List<Track>();

        foreach (var renderer in JsonWalk.FindPropertiesNamed(root, "musicTwoRowItemRenderer"))
        {
            var playlist = TryPlaylist(renderer);
            if (playlist is not null)
            {
                playlists.Add(playlist);
                continue;
            }

            var album = TryAlbum(renderer);
            if (album is not null)
                albums.Add(album);
        }

        foreach (var renderer in JsonWalk.FindPropertiesNamed(root, "musicResponsiveListItemRenderer"))
        {
            var track = TryTrack(renderer);
            if (track is not null)
                recents.Add(track);

            var playlist = TryPlaylist(renderer);
            if (playlist is not null)
                playlists.Add(playlist);
        }

        Deduplicate(playlists, albums, recents);
        return new LibrarySnapshot
        {
            Playlists = playlists,
            Albums = albums,
            Recents = recents
        };
    }

    public static HomeSnapshot ParseHome(JsonElement root)
    {
        var shelves = new List<HomeShelf>();
        foreach (var shelf in JsonWalk.FindPropertiesNamed(root, "musicCarouselShelfRenderer")
                     .Concat(JsonWalk.FindPropertiesNamed(root, "musicShelfRenderer")))
        {
            var title = shelf.TryGetProperty("header", out var header)
                ? JsonWalk.FindString(header, "text") ?? JsonWalk.Text(header)
                : JsonWalk.FindString(shelf, "title") ?? "";

            if (shelf.TryGetProperty("title", out var titleEl))
                title = string.IsNullOrWhiteSpace(title) ? JsonWalk.Text(titleEl) : title;

            var tracks = new List<Track>();
            var mixes = new List<MixSummary>();
            var albums = new List<AlbumSummary>();

            JsonWalk.Visit(shelf, el =>
            {
                if (el.ValueKind != JsonValueKind.Object)
                    return;
                if (el.TryGetProperty("musicResponsiveListItemRenderer", out var listItem))
                {
                    var t = TryTrack(listItem);
                    if (t is not null) tracks.Add(t);
                }

                if (el.TryGetProperty("musicTwoRowItemRenderer", out var twoRow))
                {
                    var mix = TryMix(twoRow);
                    if (mix is not null) mixes.Add(mix);
                    var album = TryAlbum(twoRow);
                    if (album is not null) albums.Add(album);
                    var track = TryTrack(twoRow);
                    if (track is not null) tracks.Add(track);
                }
            });

            if (tracks.Count == 0 && mixes.Count == 0 && albums.Count == 0)
                continue;

            shelves.Add(new HomeShelf
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Önerilenler" : title,
                Tracks = tracks,
                Mixes = mixes,
                Albums = albums
            });
        }

        return new HomeSnapshot { Shelves = shelves };
    }

    public static SearchSnapshot ParseSearch(string query, JsonElement root)
    {
        var songs = new List<Track>();
        var albums = new List<AlbumSummary>();
        var playlists = new List<PlaylistSummary>();

        foreach (var renderer in JsonWalk.FindPropertiesNamed(root, "musicResponsiveListItemRenderer"))
        {
            var track = TryTrack(renderer);
            if (track is not null)
                songs.Add(track);
            var playlist = TryPlaylist(renderer);
            if (playlist is not null)
                playlists.Add(playlist);
            var album = TryAlbum(renderer);
            if (album is not null)
                albums.Add(album);
        }

        foreach (var renderer in JsonWalk.FindPropertiesNamed(root, "musicTwoRowItemRenderer"))
        {
            var playlist = TryPlaylist(renderer);
            if (playlist is not null)
                playlists.Add(playlist);
            var album = TryAlbum(renderer);
            if (album is not null)
                albums.Add(album);
        }

        return new SearchSnapshot
        {
            Query = query,
            Songs = songs.DistinctBy(t => t.VideoId).ToList(),
            Albums = albums.DistinctBy(a => a.BrowseId).ToList(),
            Playlists = playlists.DistinctBy(p => p.PlaylistId).ToList()
        };
    }

    public static IReadOnlyList<Track> ParsePlaylistTracks(JsonElement root)
    {
        var tracks = new List<Track>();
        foreach (var renderer in JsonWalk.FindPropertiesNamed(root, "musicResponsiveListItemRenderer"))
        {
            var track = TryTrack(renderer);
            if (track is not null)
                tracks.Add(track);
        }

        return tracks.DistinctBy(t => t.VideoId + (t.SetVideoId ?? "")).ToList();
    }

    public static string? FindContinuationToken(JsonElement root)
    {
        foreach (var item in JsonWalk.FindPropertiesNamed(root, "continuationItemRenderer"))
        {
            if (item.TryGetProperty("continuationEndpoint", out var ep)
                && ep.TryGetProperty("continuationCommand", out var cmd)
                && cmd.TryGetProperty("token", out var tokenProp))
            {
                var tok = tokenProp.GetString();
                if (!string.IsNullOrWhiteSpace(tok))
                    return tok;
            }
        }

        foreach (var cmd in JsonWalk.FindPropertiesNamed(root, "continuationCommand"))
        {
            if (cmd.TryGetProperty("token", out var tok))
            {
                var str = tok.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                    return str;
            }
        }

        foreach (var next in JsonWalk.FindPropertiesNamed(root, "nextContinuationData"))
        {
            if (next.TryGetProperty("continuation", out var tok))
            {
                var str = tok.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                    return str;
            }
        }

        return null;
    }

    public static PlayerPayload ParsePlayer(string videoId, JsonElement root)
    {
        var title = JsonWalk.FindString(root, "title") ?? "Unknown";
        if (JsonWalk.TryGetNested(root, out var videoDetails, "videoDetails"))
        {
            title = videoDetails.TryGetProperty("title", out var t) ? t.GetString() ?? title : title;
        }

        var author = "";
        if (JsonWalk.TryGetNested(root, out videoDetails, "videoDetails")
            && videoDetails.TryGetProperty("author", out var a))
            author = a.GetString() ?? "";

        var lengthSeconds = 0;
        if (JsonWalk.TryGetNested(root, out videoDetails, "videoDetails")
            && videoDetails.TryGetProperty("lengthSeconds", out var ls)
            && int.TryParse(ls.GetString() ?? ls.ToString(), out var parsed))
            lengthSeconds = parsed;

        var thumb = JsonWalk.SmallestThumbnail(root);

        var streams = new List<AudioStreamInfo>();
        if (JsonWalk.TryGetNested(root, out var streaming, "streamingData")
            && streaming.TryGetProperty("adaptiveFormats", out var formats)
            && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in formats.EnumerateArray())
            {
                var mime = f.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "" : "";
                if (!mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                    continue;

                streams.Add(new AudioStreamInfo
                {
                    Itag = f.TryGetProperty("itag", out var itag) && itag.TryGetInt32(out var i) ? i : 0,
                    MimeType = mime,
                    Bitrate = f.TryGetProperty("bitrate", out var br) && br.TryGetInt32(out var b) ? b : 0,
                    AverageBitrate = f.TryGetProperty("averageBitrate", out var ab) && ab.TryGetInt32(out var avg) ? avg : null,
                    Url = f.TryGetProperty("url", out var url) ? url.GetString() : null,
                    SignatureCipher = f.TryGetProperty("signatureCipher", out var sc)
                        ? sc.GetString()
                        : f.TryGetProperty("cipher", out var c)
                            ? c.GetString()
                            : null,
                    AudioQuality = f.TryGetProperty("audioQuality", out var aq) ? aq.GetString() : null,
                    AudioSampleRate = f.TryGetProperty("audioSampleRate", out var sr) ? sr.GetString() : null,
                    AudioChannels = f.TryGetProperty("audioChannels", out var ch) && ch.TryGetInt32(out var nch) ? nch : null,
                    ContentLength = f.TryGetProperty("contentLength", out var cl) && long.TryParse(cl.GetString(), out var len) ? len : null
                });
            }
        }

        string? playback = null, watchtime = null, delay = null;
        if (JsonWalk.TryGetNested(root, out var tracking, "playbackTracking"))
        {
            playback = UrlFrom(tracking, "videostatsPlaybackUrl");
            watchtime = UrlFrom(tracking, "videostatsWatchtimeUrl");
            delay = UrlFrom(tracking, "videostatsDelayplayUrl");
        }

        double? loudness = null;
        if (JsonWalk.TryGetNested(root, out var audioConfig, "playerConfig", "audioConfig")
            && audioConfig.TryGetProperty("loudnessDb", out var ldb)
            && ldb.TryGetDouble(out var db))
        {
            loudness = db;
        }

        return new PlayerPayload
        {
            Track = new Track
            {
                VideoId = videoId,
                Title = title,
                Artists = author,
                Duration = TimeSpan.FromSeconds(lengthSeconds),
                ThumbnailUrl = thumb
            },
            AudioStreams = streams.OrderByDescending(s => s.Bitrate).ToList(),
            VideostatsPlaybackUrl = playback,
            VideostatsWatchtimeUrl = watchtime,
            VideostatsDelayplayUrl = delay,
            LoudnessDb = loudness,
            DurationMs = lengthSeconds * 1000
        };
    }

    public static PlayerPayload MergePlayers(PlayerPayload web, PlayerPayload android)
    {
        var streams = new List<AudioStreamInfo>(web.AudioStreams.Count + android.AudioStreams.Count);
        streams.AddRange(web.AudioStreams);
        foreach (var stream in android.AudioStreams)
        {
            if (streams.Any(existing => existing.Itag == stream.Itag
                && string.Equals(existing.Url, stream.Url, StringComparison.Ordinal)
                && string.Equals(existing.MimeType, stream.MimeType, StringComparison.Ordinal)))
                continue;
            streams.Add(stream);
        }

        return new PlayerPayload
        {
            Track = web.Track,
            AudioStreams = streams
                .OrderByDescending(s => s.Bitrate)
                .ToList(),
            VideostatsPlaybackUrl = web.VideostatsPlaybackUrl ?? android.VideostatsPlaybackUrl,
            VideostatsWatchtimeUrl = web.VideostatsWatchtimeUrl ?? android.VideostatsWatchtimeUrl,
            VideostatsDelayplayUrl = web.VideostatsDelayplayUrl ?? android.VideostatsDelayplayUrl,
            LoudnessDb = web.LoudnessDb ?? android.LoudnessDb,
            DurationMs = web.DurationMs > 0 ? web.DurationMs : android.DurationMs,
            PlaybackTrackingWatchUrl = web.PlaybackTrackingWatchUrl ?? android.PlaybackTrackingWatchUrl
        };
    }

    public static AudioStreamInfo? SelectPreferredAudio(IReadOnlyList<AudioStreamInfo> streams)
    {
        var candidate = streams.FirstOrDefault(s => s.Itag == 251)
               ?? streams.Where(s => s.IsOpus).OrderByDescending(s => s.Bitrate).FirstOrDefault()
               ?? streams.FirstOrDefault(s => s.Itag == 140)
               ?? streams.Where(s => s.IsAac).OrderByDescending(s => s.Bitrate).FirstOrDefault()
               ?? streams.OrderByDescending(s => s.Bitrate).FirstOrDefault();

        if (candidate is null)
            return null;

        if (string.IsNullOrWhiteSpace(candidate.Url) && !string.IsNullOrWhiteSpace(candidate.SignatureCipher))
        {
            var decoded = TryDecodeCipher(candidate.SignatureCipher);
            if (!string.IsNullOrWhiteSpace(decoded))
                return candidate with { Url = decoded };
        }

        return candidate;
    }

    private static string? TryDecodeCipher(string cipher)
    {
        var map = cipher.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(part => part.Length == 2)
            .ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]), StringComparer.OrdinalIgnoreCase);

        if (!map.TryGetValue("url", out var url) || string.IsNullOrWhiteSpace(url))
            return null;

        if (map.TryGetValue("sp", out var sp) && string.Equals(sp, "sig", StringComparison.OrdinalIgnoreCase)
            && map.TryGetValue("s", out var sig) && !string.IsNullOrWhiteSpace(sig))
        {
            var delim = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            return url + delim + "sig=" + Uri.EscapeDataString(sig);
        }

        return url;
    }

    private static string? UrlFrom(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var node))
            return null;
        if (node.TryGetProperty("baseUrl", out var baseUrl))
            return baseUrl.GetString();
        return node.GetString();
    }

    private static Track? TryTrack(JsonElement renderer)
    {
        var videoId = JsonWalk.FindString(renderer, "videoId");
        if (string.IsNullOrWhiteSpace(videoId))
            return null;

        var title = ReadText(renderer, "title", "videoTitle", "name")
            ?? FirstColumnText(renderer, 0)
            ?? videoId;
        var artists = ReadText(renderer, "artistText", "longBylineText", "subtitle", "byline", "ownerText")
            ?? FirstColumnText(renderer, 1)
            ?? "";
        var extra = FirstColumnText(renderer, 2) ?? ReadText(renderer, "lengthText", "durationText", "duration");
        var durationText = extra ?? JsonWalk.FindString(renderer, "lengthText");
        var setVideoId = JsonWalk.FindString(renderer, "setVideoId");
        var playlistId = JsonWalk.FindString(renderer, "playlistId");

        return new Track
        {
            VideoId = videoId,
            Title = title,
            Artists = artists,
            Duration = JsonWalk.ParseDuration(durationText),
            ThumbnailUrl = JsonWalk.SmallestThumbnail(renderer),
            PlaylistId = playlistId,
            SetVideoId = setVideoId
        };
    }

    private static PlaylistSummary? TryPlaylist(JsonElement renderer)
    {
        string? browseId = null;
        string? playlistId = null;

        if (renderer.TryGetProperty("navigationEndpoint", out var nav) && nav.TryGetProperty("browseEndpoint", out var be))
        {
            if (be.TryGetProperty("browseId", out var bId))
                browseId = bId.GetString();
        }

        if (string.IsNullOrWhiteSpace(browseId) && renderer.TryGetProperty("title", out var titleEl) && titleEl.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in runs.EnumerateArray())
            {
                if (r.TryGetProperty("navigationEndpoint", out var rNav) && rNav.TryGetProperty("browseEndpoint", out var rBe))
                {
                    if (rBe.TryGetProperty("browseId", out var rBid))
                    {
                        browseId = rBid.GetString();
                        break;
                    }
                }
            }
        }

        browseId ??= JsonWalk.FindString(renderer, "browseId");
        playlistId ??= JsonWalk.FindString(renderer, "playlistId");

        if (string.IsNullOrWhiteSpace(playlistId) && !string.IsNullOrWhiteSpace(browseId))
        {
            playlistId = browseId.StartsWith("VL", StringComparison.Ordinal) ? browseId[2..] : browseId;
        }

        if (string.IsNullOrWhiteSpace(playlistId))
            return null;

        // Skip albums or audio releases
        if (playlistId.StartsWith("MPREb_", StringComparison.Ordinal) || playlistId.StartsWith("OLAK5uy_", StringComparison.Ordinal))
            return null;

        var title = (renderer.TryGetProperty("title", out var tEl) ? JsonWalk.Text(tEl) : null)
            ?? ReadText(renderer, "title", "name")
            ?? FirstColumnText(renderer, 0)
            ?? JsonWalk.FindString(renderer, "title")
            ?? playlistId;

        // Extract track count from subtitle / secondaryText if present
        var subtitle = ReadText(renderer, "subtitle", "secondaryText", "shortBylineText")
                       ?? FirstColumnText(renderer, 1) ?? "";

        int count = 0;
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var match = Regex.Match(subtitle, @"(\d+)\s*(şarkı|parça|song|track|video|öğe)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var c))
                count = c;
        }

        var isLiked = playlistId is "LM" || (title?.Contains("Liked", StringComparison.OrdinalIgnoreCase) == true)
                      || title?.Contains("Beğenilen", StringComparison.OrdinalIgnoreCase) == true;

        return new PlaylistSummary
        {
            PlaylistId = playlistId!,
            Title = title ?? playlistId!,
            ThumbnailUrl = JsonWalk.SmallestThumbnail(renderer),
            TrackCount = count,
            IsLikedSongs = isLiked,
            IsOwn = true
        };
    }

    private static AlbumSummary? TryAlbum(JsonElement renderer)
    {
        var browseId = JsonWalk.FindString(renderer, "browseId");
        if (string.IsNullOrWhiteSpace(browseId) || !browseId.StartsWith("MPREb_", StringComparison.Ordinal))
            return null;

        return new AlbumSummary
        {
            BrowseId = browseId,
            Title = (renderer.TryGetProperty("title", out var titleEl) ? JsonWalk.Text(titleEl) : null)
                ?? ReadText(renderer, "title", "name")
                ?? FirstColumnText(renderer, 0)
                ?? JsonWalk.FindString(renderer, "title")
                ?? browseId,
            Artists = FirstColumnText(renderer, 1) ?? "",
            ThumbnailUrl = JsonWalk.SmallestThumbnail(renderer),
            PlaylistId = JsonWalk.FindString(renderer, "playlistId")
        };
    }

    private static MixSummary? TryMix(JsonElement renderer)
    {
        var browseId = JsonWalk.FindString(renderer, "browseId") ?? JsonWalk.FindString(renderer, "playlistId");
        var title = ReadText(renderer, "title", "name") ?? FirstColumnText(renderer, 0);
        if (string.IsNullOrWhiteSpace(browseId) || string.IsNullOrWhiteSpace(title))
            return null;

        var looksLikeMix = title.Contains("Mix", StringComparison.OrdinalIgnoreCase)
                           || title.Contains("Radio", StringComparison.OrdinalIgnoreCase)
                           || browseId.StartsWith("RDAM", StringComparison.Ordinal)
                           || browseId.Contains("radio", StringComparison.OrdinalIgnoreCase);
        if (!looksLikeMix)
            return null;

        return new MixSummary
        {
            BrowseId = browseId,
            Title = title,
            Subtitle = ReadText(renderer, "subtitle", "secondaryText", "longBylineText") ?? FirstColumnText(renderer, 1) ?? "",
            ThumbnailUrl = JsonWalk.SmallestThumbnail(renderer),
            PlaylistId = JsonWalk.FindString(renderer, "playlistId")
        };
    }

    private static string? ReadText(JsonElement renderer, params string[] names)
    {
        foreach (var name in names)
        {
            if (renderer.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(JsonWalk.Text(value)))
                return JsonWalk.Text(value);
        }

        foreach (var prop in renderer.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Object && !string.IsNullOrWhiteSpace(JsonWalk.Text(prop.Value)))
                return JsonWalk.Text(prop.Value);
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray())
                {
                    if (!string.IsNullOrWhiteSpace(JsonWalk.Text(item)))
                        return JsonWalk.Text(item);
                }
            }
        }

        return null;
    }

    private static string? FirstColumnText(JsonElement renderer, int index)
    {
        if (!renderer.TryGetProperty("flexColumns", out var cols) || cols.ValueKind != JsonValueKind.Array)
            return null;
        var arr = cols.EnumerateArray().ToArray();
        if (index >= arr.Length)
            return null;
        var col = arr[index];
        if (col.TryGetProperty("musicResponsiveListItemFlexColumnRenderer", out var flex)
            && flex.TryGetProperty("text", out var text))
            return JsonWalk.Text(text);
        return JsonWalk.Text(col);
    }

    private static void Deduplicate(List<PlaylistSummary> playlists, List<AlbumSummary> albums, List<Track> recents)
    {
        var p = playlists.DistinctBy(x => x.PlaylistId).ToList();
        playlists.Clear();
        playlists.AddRange(p);

        var a = albums.DistinctBy(x => x.BrowseId).ToList();
        albums.Clear();
        albums.AddRange(a);

        var r = recents.DistinctBy(x => x.VideoId).ToList();
        recents.Clear();
        recents.AddRange(r);
    }
}

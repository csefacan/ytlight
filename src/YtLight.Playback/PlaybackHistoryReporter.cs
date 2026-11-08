using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using YtLight.Core.Abstractions;
using YtLight.Core.Models;
using YtLight.Core.Session;
using YtLight.InnerTube;
using YtLight.InnerTube.Auth;

namespace YtLight.Playback;

public sealed class PlaybackHistoryReporter : IPlaybackHistoryReporter
{
    private readonly HttpClient _http;
    private readonly InnerTubeOptions _options;
    private readonly ILogger<PlaybackHistoryReporter> _logger;
    private AuthSession? _session;
    private PlayerPayload? _current;
    private string? _cpn;
    private bool _playbackPingSent;

    public PlaybackHistoryReporter(HttpClient http, InnerTubeOptions options, ILogger<PlaybackHistoryReporter> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public void BindSession(AuthSession? session) => _session = session;

    public async Task StartAsync(PlayerPayload player, CancellationToken cancellationToken = default)
    {
        _current = player;
        _playbackPingSent = false;
        _cpn = GenerateCpn();

        if (string.IsNullOrWhiteSpace(player.VideostatsPlaybackUrl))
            return;

        var url = AppendCpn(player.VideostatsPlaybackUrl, _cpn);
        await SendAsync(url, cancellationToken).ConfigureAwait(false);
        _playbackPingSent = true;
    }

    public async Task ReportWatchtimeAsync(TimeSpan position, TimeSpan duration, bool paused, CancellationToken cancellationToken = default)
    {
        if (_current?.VideostatsWatchtimeUrl is null || _cpn is null)
            return;

        var rt = paused ? "paused" : "playing";
        var pos = Math.Max(0, position.TotalSeconds);
        var dur = Math.Max(pos, duration.TotalSeconds);
        var url = AppendCpn(_current.VideostatsWatchtimeUrl, _cpn);
        url += $"&rt={rt}&cmt={pos:0.000}&len={dur:0.000}&st={Math.Max(0, pos - 10):0.000}&et={pos:0.000}";
        await SendAsync(url, cancellationToken).ConfigureAwait(false);
        _ = _playbackPingSent;
    }

    public Task StopAsync()
    {
        _current = null;
        _cpn = null;
        _playbackPingSent = false;
        return Task.CompletedTask;
    }

    private async Task SendAsync(string url, CancellationToken cancellationToken)
    {
        var session = _session;
        if (session is null)
            return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
            request.Headers.TryAddWithoutValidation("Origin", _options.Origin);
            request.Headers.TryAddWithoutValidation("Referer", _options.Origin + "/");
            request.Headers.TryAddWithoutValidation("Cookie", session.ToCookieHeader());
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(
                SapisidHash.BuildAuthorizationHeader(session.Sapisid, session.Secure1Papisid, session.Secure3Papisid, _options.Origin));

            var profile = session.ActiveProfile;
            if (profile?.HasPageIsolation == true)
            {
                var pageId = profile.PageId ?? profile.OnBehalfOfUser;
                if (!string.IsNullOrWhiteSpace(pageId))
                    request.Headers.TryAddWithoutValidation("X-Goog-PageId", pageId);
            }

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                _logger.LogDebug("Watchtime ping {Status}", (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "History ping skipped");
        }
    }

    private static string AppendCpn(string url, string cpn)
    {
        var joiner = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        if (url.Contains("cpn=", StringComparison.Ordinal))
            return url;
        return url + joiner + "cpn=" + cpn;
    }

    private static string GenerateCpn()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";
        Span<char> buffer = stackalloc char[16];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        return new string(buffer);
    }
}

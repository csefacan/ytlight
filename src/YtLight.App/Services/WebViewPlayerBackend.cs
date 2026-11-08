using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using YtLight.Core.Abstractions;

namespace YtLight.App.Services;

public sealed class WebViewPlayerBackend : IAudioPlayerBackend
{
    private readonly Dispatcher _dispatcher;
    private WebView2? _webView;
    private bool _initialized;
    private string? _currentVideoId;

    private readonly object _stateGate = new();
    private bool _isPlaying;
    private TimeSpan _position = TimeSpan.Zero;
    private TimeSpan _duration = TimeSpan.Zero;
    private bool _ended;
    private DateTimeOffset _lastPlayAttempt = DateTimeOffset.MinValue;

    public WebViewPlayerBackend()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public void Attach(WebView2 webView)
    {
        _webView = webView;
        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        if (_initialized || _webView == null)
            return;

        await _dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var env = await WebViewEnvironmentManager.GetOrCreateSharedEnvironmentAsync();
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

                _webView.CoreWebView2.ScriptDialogOpening += (s, e) =>
                {
                    // Auto-accept any script dialog to prevent modal prompts from blocking
                    e.Accept();
                };

                // Script to dismiss cookie popups, accept prompts, and prevent beforeunload prompts
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    window.onbeforeunload = null;
                    window.addEventListener('beforeunload', function(e) {
                        e.preventDefault();
                        delete e['returnValue'];
                    }, true);

                    window.addEventListener('DOMContentLoaded', function() {
                        setInterval(function() {
                            var btn = document.querySelector('button[aria-label*=""Kabul""], button[aria-label*=""Accept""], [aria-label*=""Dismiss""], yt-button-renderer#dismiss-button, .ytp-skip-ad-button, .ytp-ad-skip-button, .ytp-ad-skip-button-modern');
                            if (btn) btn.click();
                            var v = document.querySelector('video');
                            if (v && v.paused && !v.ended) {
                                v.play().catch(function(){});
                            }
                        }, 500);
                    });
                ");

                _initialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebViewPlayerBackend] Init failed: {ex}");
            }
        });
    }

    public async Task PlayVideoAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return;

        _currentVideoId = videoId;
        lock (_stateGate)
        {
            _ended = false;
            _position = TimeSpan.Zero;
            _isPlaying = true;
            _lastPlayAttempt = DateTimeOffset.UtcNow;
        }

        await _dispatcher.InvokeAsync(async () =>
        {
            if (_webView?.CoreWebView2 == null)
                await InitializeAsync();

            if (_webView?.CoreWebView2 != null)
            {
                // First try to load video seamlessly via movie_player API without full page reload
                var script = $@"
                    (function() {{
                        try {{
                            var p = document.getElementById('movie_player');
                            if (p && typeof p.loadVideoById === 'function') {{
                                p.loadVideoById('{videoId}');
                                p.playVideo();
                                return 'loaded';
                            }}
                        }} catch(e) {{}}
                        return 'navigate';
                    }})();
                ";

                var result = await _webView.ExecuteScriptAsync(script);
                var res = result?.Trim('"', ' ', '\r', '\n');
                if (res != "loaded")
                {
                    var targetUrl = "https://music.youtube.com/watch?v=" + videoId;
                    _webView.CoreWebView2.Navigate(targetUrl);
                }
            }
        });
    }

    public async Task PlayPauseAsync()
    {
        await _dispatcher.InvokeAsync(async () =>
        {
            if (_webView?.CoreWebView2 == null) return;
            await _webView.ExecuteScriptAsync(@"
                (function() {
                    var p = document.getElementById('movie_player');
                    if (p && typeof p.getPlayerState === 'function') {
                        var state = p.getPlayerState();
                        if (state === 1) p.pauseVideo();
                        else p.playVideo();
                        return;
                    }
                    var v = document.querySelector('video');
                    if (v) {
                        if (v.paused) v.play().catch(function(){});
                        else v.pause();
                    }
                })()
            ");
        });
    }

    public async Task SeekAsync(TimeSpan position)
    {
        var secs = Math.Max(0, position.TotalSeconds);
        lock (_stateGate) _position = position;

        await _dispatcher.InvokeAsync(async () =>
        {
            if (_webView?.CoreWebView2 == null) return;
            var secStr = secs.ToString(CultureInfo.InvariantCulture);
            await _webView.ExecuteScriptAsync($@"
                (function() {{
                    var p = document.getElementById('movie_player');
                    if (p && typeof p.seekTo === 'function') {{
                        p.seekTo({secStr}, true);
                        return;
                    }}
                    var v = document.querySelector('video');
                    if (v) {{ v.currentTime = {secStr}; }}
                }})()
            ");
        });
    }

    public async Task SetVolumeAsync(double volume)
    {
        var vol = Math.Clamp(volume, 0.0, 1.0);
        var pct = (int)Math.Round(vol * 100);
        var volStr = vol.ToString(CultureInfo.InvariantCulture);

        await _dispatcher.InvokeAsync(async () =>
        {
            if (_webView?.CoreWebView2 == null) return;
            await _webView.ExecuteScriptAsync($@"
                (function() {{
                    var p = document.getElementById('movie_player');
                    if (p && typeof p.setVolume === 'function') {{
                        p.setVolume({pct});
                        if ({pct} > 0 && typeof p.unMute === 'function') p.unMute();
                    }}
                    var v = document.querySelector('video');
                    if (v) {{ v.volume = {volStr}; }}
                }})()
            ");
        });
    }

    public async Task SetMutedAsync(bool isMuted)
    {
        await _dispatcher.InvokeAsync(async () =>
        {
            if (_webView?.CoreWebView2 == null) return;
            var mutedBool = isMuted ? "true" : "false";
            await _webView.ExecuteScriptAsync($@"
                (function() {{
                    var p = document.getElementById('movie_player');
                    if (p) {{
                        if ({mutedBool} && typeof p.mute === 'function') p.mute();
                        else if (!{mutedBool} && typeof p.unMute === 'function') p.unMute();
                    }}
                    var v = document.querySelector('video');
                    if (v) {{ v.muted = {mutedBool}; }}
                }})()
            ");
        });
    }

    public (bool isPlaying, TimeSpan position, TimeSpan duration, bool ended) QueryState()
    {
        if (_initialized && _webView?.CoreWebView2 != null)
        {
            _ = _dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var json = await _webView.ExecuteScriptAsync(@"
                        (function() {
                            var p = document.getElementById('movie_player');
                            if (p && typeof p.getCurrentTime === 'function' && typeof p.getDuration === 'function') {
                                var state = p.getPlayerState();
                                var cur = p.getCurrentTime() || 0;
                                var dur = p.getDuration() || 0;
                                var ended = state === 0;
                                var paused = state === 2 || state === -1 || ended;
                                return JSON.stringify({ paused: paused, cur: cur, dur: dur, ended: ended });
                            }
                            var v = document.querySelector('video');
                            if (!v) return null;
                            return JSON.stringify({
                                paused: v.paused,
                                cur: v.currentTime || 0,
                                dur: v.duration || 0,
                                ended: v.ended
                            });
                        })()
                    ");

                    if (!string.IsNullOrWhiteSpace(json) && json != "null" && json != "\"null\"")
                    {
                        var unescaped = json.StartsWith('"') && json.EndsWith('"')
                            ? JsonSerializer.Deserialize<string>(json)
                            : json;

                        if (!string.IsNullOrWhiteSpace(unescaped) && unescaped != "null")
                        {
                            using var doc = JsonDocument.Parse(unescaped);
                            var root = doc.RootElement;
                            var paused = root.GetProperty("paused").GetBoolean();
                            var curSec = root.GetProperty("cur").GetDouble();
                            var durSec = root.GetProperty("dur").GetDouble();
                            var ended = root.GetProperty("ended").GetBoolean();

                            lock (_stateGate)
                            {
                                _isPlaying = !paused;
                                _position = TimeSpan.FromSeconds(curSec);
                                if (durSec > 0)
                                    _duration = TimeSpan.FromSeconds(durSec);
                                _ended = ended;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore query race
                }
            });
        }

        lock (_stateGate)
        {
            return (_isPlaying, _position, _duration, _ended);
        }
    }

    public void Dispose()
    {
        _webView?.Dispose();
        _webView = null;
    }
}

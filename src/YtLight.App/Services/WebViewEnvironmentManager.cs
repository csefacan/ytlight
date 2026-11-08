using System.IO;
using Microsoft.Web.WebView2.Core;

namespace YtLight.App.Services;

public static class WebViewEnvironmentManager
{
    private static readonly SemaphoreSlim _gate = new(1, 1);
    private static CoreWebView2Environment? _sharedEnvironment;

    public static string ProfileFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YtLight", "WebViewProfile");

    public static async Task<CoreWebView2Environment> GetOrCreateSharedEnvironmentAsync()
    {
        if (_sharedEnvironment != null)
            return _sharedEnvironment;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_sharedEnvironment != null)
                return _sharedEnvironment;

            Directory.CreateDirectory(ProfileFolder);

            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required --disable-background-timer-throttling --disable-renderer-backgrounding"
            };

            _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, ProfileFolder, options).ConfigureAwait(false);
            return _sharedEnvironment;
        }
        finally
        {
            _gate.Release();
        }
    }
}

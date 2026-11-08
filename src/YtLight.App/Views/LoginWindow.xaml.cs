using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using YtLight.App.Services;
using YtLight.Core.Session;

namespace YtLight.App.Views;

public partial class LoginWindow : Window
{
    private bool _captured;

    public AuthSession? CapturedSession { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await WebViewEnvironmentManager.GetOrCreateSharedEnvironmentAsync();
            await Browser.EnsureCoreWebView2Async(env);
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.NavigationCompleted += OnNavigationCompleted;
            Browser.SourceChanged += OnSourceChanged;

            Browser.CoreWebView2.Navigate(
                "https://accounts.google.com/ServiceLogin?service=youtube&passive=true&continue=https://music.youtube.com/");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Giriş sayfası açılamadı: " + ex.Message;
            MessageBox.Show(this,
                $"WebView2 başlatılırken hata oluştu:\n{ex.Message}\n\nLütfen Microsoft Edge WebView2 Runtime bileşeninin sisteminizde yüklü olduğunu kontrol edin.",
                "YtLight — Başlatma Hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private async void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        await CheckAuthAsync();
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            await CheckAuthAsync();
        }
    }

    private async Task CheckAuthAsync()
    {
        if (_captured || Browser?.CoreWebView2 is null || Browser.Source is null)
            return;

        var host = Browser.Source.Host;
        // MUST be on music.youtube.com, NOT intermediate accounts.google.com or accounts.youtube.com
        if (!string.Equals(host, "music.youtube.com", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var cookieManager = Browser.CoreWebView2.CookieManager;
            var cookies = new List<CoreWebView2Cookie>();
            cookies.AddRange(await cookieManager.GetCookiesAsync("https://music.youtube.com"));
            cookies.AddRange(await cookieManager.GetCookiesAsync("https://www.youtube.com"));
            cookies.AddRange(await cookieManager.GetCookiesAsync("https://youtube.com"));
            cookies.AddRange(await cookieManager.GetCookiesAsync("https://accounts.google.com"));

            var mapped = cookies
                .GroupBy(c => c.Name + "@" + c.Domain)
                .Select(g => g.Last())
                .Select(c => new StoredCookie
                {
                    Name = c.Name,
                    Value = c.Value,
                    Domain = c.Domain,
                    Path = c.Path,
                    Secure = c.IsSecure,
                    HttpOnly = c.IsHttpOnly
                })
                .ToList();

            var hasSapisid = mapped.Any(c => c.Name is "SAPISID" or "__Secure-3PAPISID" or "__Secure-1PAPISID");
            var hasSid = mapped.Any(c => c.Name is "SID" or "__Secure-3PSID" or "__Secure-1PSID" or "LOGIN_INFO");

            if (!hasSapisid || !hasSid)
                return;

            _captured = true;
            StatusText.Text = "Giriş başarılı! Profil bilgileri yükleniyor...";

            string? visitor = null;
            try
            {
                var scriptResult = await Browser.ExecuteScriptAsync(
                    "(() => { try { return window.ytcfg && ytcfg.get && (ytcfg.get('VISITOR_DATA') || ''); } catch(e) { return ''; } })()");
                visitor = JsonSerializer.Deserialize<string>(scriptResult);
            }
            catch
            {
                // Best effort
            }

            if (string.IsNullOrWhiteSpace(visitor))
                visitor = mapped.LastOrDefault(c => c.Name == "VISITOR_INFO1_LIVE")?.Value;

            CapturedSession = new AuthSession
            {
                Cookies = mapped,
                VisitorData = visitor,
                SavedAt = DateTimeOffset.UtcNow
            };

            await Task.Delay(600);

            try
            {
                if (IsLoaded)
                {
                    DialogResult = true;
                }
            }
            catch (InvalidOperationException)
            {
                // Window was already closed
            }
        }
        catch
        {
            // Ignore background check transient errors
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        try
        {
            if (Browser?.CoreWebView2 is not null)
            {
                Browser.NavigationCompleted -= OnNavigationCompleted;
                Browser.SourceChanged -= OnSourceChanged;
            }
            Browser?.Dispose();
        }
        catch
        {
            // Disposed
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }
}

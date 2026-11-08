using System.Net;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YtLight.App.Services;
using YtLight.App.ViewModels;
using YtLight.Core;
using YtLight.Core.Abstractions;
using YtLight.Core.Memory;
using YtLight.InnerTube;
using YtLight.InnerTube.Auth;
using YtLight.Playback;

namespace YtLight.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private ITrayService? _tray;
    private ISmtcService? _smtc;

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[DispatcherUnhandledException] {e.Exception}");
            e.Handled = true;
            MessageBox.Show($"Uygulama hatası:\n{e.Exception.Message}", "YtLight Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[AppDomain UnhandledException] {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {e.Exception}");
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = BuildServices();

        var memory = _services.GetRequiredService<MemoryGuardian>();
        var thumbnails = _services.GetRequiredService<ThumbnailCache>();
        memory.TrimRequested += (_, _) => thumbnails.Clear();

        var engine = _services.GetRequiredService<IAudioEngine>();

        try
        {
            _smtc = _services.GetRequiredService<ISmtcService>();
            _smtc.Attach(engine);
        }
        catch
        {
            // SMTC fallback
        }

        try
        {
            _tray = _services.GetRequiredService<ITrayService>();
        }
        catch
        {
            // Tray fallback
        }

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _smtc?.Dispose();
        _tray?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton<InnerTubeOptions>();
        services.AddSingleton<ThumbnailCache>();
        services.AddSingleton<MemoryGuardian>();
        services.AddSingleton<ISessionStore, DpapiSessionStore>();

        services.AddHttpClient("yt")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(4),
                MaxConnectionsPerServer = 4
            });

        services.AddSingleton<YoutubeMusicBootstrapper>(sp =>
            new YoutubeMusicBootstrapper(sp.GetRequiredService<IHttpClientFactory>().CreateClient("yt")));
        services.AddSingleton<IInnerTubeClient>(sp =>
            new InnerTubeClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("yt"),
                sp.GetRequiredService<InnerTubeOptions>(),
                sp.GetRequiredService<YoutubeMusicBootstrapper>(),
                sp.GetRequiredService<ILogger<InnerTubeClient>>()));
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<PlaybackHistoryReporter>(sp =>
            new PlaybackHistoryReporter(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("yt"),
                sp.GetRequiredService<InnerTubeOptions>(),
                sp.GetRequiredService<ILogger<PlaybackHistoryReporter>>()));
        services.AddSingleton<IPlaybackHistoryReporter>(sp => sp.GetRequiredService<PlaybackHistoryReporter>());

        services.AddSingleton<WebViewPlayerBackend>();
        services.AddSingleton<IAudioPlayerBackend>(sp => sp.GetRequiredService<WebViewPlayerBackend>());
        services.AddSingleton<IAudioEngine>(sp => new AudioEngine(
            sp.GetRequiredService<IAudioPlayerBackend>(),
            sp.GetRequiredService<IInnerTubeClient>(),
            sp.GetRequiredService<IPlaybackHistoryReporter>()));

        services.AddSingleton<ISleepTimer, SleepTimerService>();
        services.AddSingleton<ISmtcService, SmtcService>();
        services.AddSingleton<ITrayService, TrayService>();

        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<ExploreViewModel>();
        services.AddSingleton<PlayerBarViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }
}

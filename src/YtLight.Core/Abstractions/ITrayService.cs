namespace YtLight.Core.Abstractions;

public interface ITrayService : IDisposable
{
    void Show();
    void HideBalloon();
    event EventHandler? ShowWindowRequested;
    event EventHandler? PlayPauseRequested;
    event EventHandler? NextRequested;
    event EventHandler? SleepTimerRequested;
    event EventHandler? ExitRequested;
}

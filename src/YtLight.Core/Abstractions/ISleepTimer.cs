using YtLight.Core.Models;

namespace YtLight.Core.Abstractions;

public interface ISleepTimer
{
    SleepTimerMode Mode { get; }
    TimeSpan? Remaining { get; }
    bool ExitAppWhenDone { get; set; }
    event EventHandler? Tick;
    event EventHandler? Completed;

    void Start(SleepTimerMode mode, TimeSpan? customDuration = null);
    void Cancel();
}

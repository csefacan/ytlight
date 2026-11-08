using YtLight.Core.Abstractions;
using YtLight.Core.Models;

namespace YtLight.Playback;

public sealed class SleepTimerService : ISleepTimer, IDisposable
{
    private readonly IAudioEngine _engine;
    private CancellationTokenSource? _cts;
    private DateTimeOffset? _deadline;

    public SleepTimerService(IAudioEngine engine) => _engine = engine;

    public SleepTimerMode Mode { get; private set; } = SleepTimerMode.Off;
    public TimeSpan? Remaining =>
        _deadline is null ? null : _deadline.Value - DateTimeOffset.UtcNow;
    public bool ExitAppWhenDone { get; set; }
    public event EventHandler? Tick;
    public event EventHandler? Completed;

    public void Start(SleepTimerMode mode, TimeSpan? customDuration = null)
    {
        Cancel();
        Mode = mode;
        if (mode == SleepTimerMode.Off)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (mode == SleepTimerMode.EndOfTrack)
        {
            _engine.PlaybackEnded += OnTrackEnded;
            return;
        }

        var duration = mode == SleepTimerMode.Custom
            ? customDuration ?? TimeSpan.FromMinutes(30)
            : TimeSpan.FromMinutes((int)mode);
        _deadline = DateTimeOffset.UtcNow + duration;
        _ = RunAsync(duration, token);
    }

    public void Cancel()
    {
        _engine.PlaybackEnded -= OnTrackEnded;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _deadline = null;
        Mode = SleepTimerMode.Off;
    }

    private async Task RunAsync(TimeSpan duration, CancellationToken token)
    {
        var remaining = duration;
        try
        {
            while (remaining > TimeSpan.Zero)
            {
                var slice = remaining > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : remaining;
                await Task.Delay(slice, token).ConfigureAwait(false);
                remaining -= slice;
                Tick?.Invoke(this, EventArgs.Empty);
            }

            _engine.FadeOutAndStop(TimeSpan.FromSeconds(8));
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            // cancelled by user
        }
    }

    private void OnTrackEnded(object? sender, EventArgs e)
    {
        _engine.PlaybackEnded -= OnTrackEnded;
        _engine.FadeOutAndStop(TimeSpan.FromSeconds(4));
        Completed?.Invoke(this, EventArgs.Empty);
        Mode = SleepTimerMode.Off;
    }

    public void Dispose() => Cancel();
}

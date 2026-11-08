using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtLight.Core.Abstractions;
using YtLight.Core.Models;

namespace YtLight.App.ViewModels;

public partial class PlayerBarViewModel : ObservableObject
{
    private readonly IAudioEngine _engine;
    private readonly ISleepTimer _sleepTimer;
    private readonly IInnerTubeClient _client;

    public PlayerBarViewModel(IAudioEngine engine, ISleepTimer sleepTimer, IInnerTubeClient client)
    {
        _engine = engine;
        _sleepTimer = sleepTimer;
        _client = client;
        _engine.StateChanged += (_, _) => Refresh();
        _engine.TrackChanged += (_, _) => Refresh();
        _engine.QueueChanged += (_, _) => RefreshQueue();
        _sleepTimer.Tick += (_, _) => OnPropertyChanged(nameof(SleepLabel));
        _sleepTimer.Completed += (_, _) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(SleepLabel));
                if (_sleepTimer.ExitAppWhenDone)
                    System.Windows.Application.Current.Shutdown();
            });
        };
        Refresh();
    }

    public ObservableCollection<QueueItem> Queue { get; } = [];

    [ObservableProperty] private Track? currentTrack;
    [ObservableProperty] private bool isPlaying;
    [ObservableProperty] private bool shuffle;
    [ObservableProperty] private RepeatMode repeatMode;
    [ObservableProperty] private double volume = 0.85;
    [ObservableProperty] private bool isMuted;
    [ObservableProperty] private double positionSeconds;
    [ObservableProperty] private double durationSeconds;
    [ObservableProperty] private bool isQueueOpen;

    partial void OnVolumeChanged(double value)
    {
        if (Math.Abs(_engine.Volume - value) > 0.005)
            _engine.Volume = value;
    }

    public string PositionText => TimeSpan.FromSeconds(PositionSeconds).ToString(@"m\:ss");
    public string DurationText => TimeSpan.FromSeconds(DurationSeconds).ToString(@"m\:ss");
    public string RepeatLabel => RepeatMode switch
    {
        RepeatMode.All => "Tekrar: Liste",
        RepeatMode.One => "Tekrar: Parça",
        _ => "Tekrar: Kapalı"
    };
    public string RepeatIcon => RepeatMode switch
    {
        RepeatMode.All => "🔁",
        RepeatMode.One => "🔂",
        _ => "🔁"
    };
    public string SleepLabel =>
        _sleepTimer.Mode == SleepTimerMode.Off
            ? "Uyku"
            : _sleepTimer.Mode == SleepTimerMode.EndOfTrack
                ? "Uyku: parça sonu"
                : _sleepTimer.Remaining is { } left && left > TimeSpan.Zero
                    ? $"Uyku {left:mm\\:ss}"
                    : "Uyku";

    public bool ExitAppWhenDone
    {
        get => _sleepTimer.ExitAppWhenDone;
        set => _sleepTimer.ExitAppWhenDone = value;
    }

    private void Refresh()
    {
        CurrentTrack = _engine.CurrentTrack;
        IsPlaying = _engine.IsPlaying;
        Shuffle = _engine.Shuffle;
        RepeatMode = _engine.RepeatMode;
        if (Math.Abs(Volume - _engine.Volume) > 0.005)
            Volume = _engine.Volume;
        IsMuted = _engine.IsMuted;
        PositionSeconds = _engine.Position.TotalSeconds;
        DurationSeconds = Math.Max(1, _engine.Duration.TotalSeconds);
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(RepeatLabel));
        OnPropertyChanged(nameof(RepeatIcon));
    }

    private void RefreshQueue()
    {
        Queue.Clear();
        foreach (var item in _engine.Queue)
            Queue.Add(item);
    }

    [RelayCommand]
    private Task PlayPauseAsync() => _engine.PlayPauseAsync();

    [RelayCommand]
    private Task NextAsync() => _engine.NextAsync();

    [RelayCommand]
    private Task PreviousAsync() => _engine.PreviousAsync();

    [RelayCommand]
    private void ToggleShuffle()
    {
        _engine.Shuffle = !_engine.Shuffle;
        Shuffle = _engine.Shuffle;
    }

    [RelayCommand]
    private void CycleRepeat()
    {
        _engine.RepeatMode = _engine.RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.Off
        };
        RepeatMode = _engine.RepeatMode;
        Refresh();
    }

    [RelayCommand]
    private void ToggleMute() => _engine.IsMuted = !_engine.IsMuted;

    [RelayCommand]
    private void Seek(double seconds) => _engine.Seek(TimeSpan.FromSeconds(seconds));

    [RelayCommand]
    private void ApplyVolume(double value) => _engine.Volume = value;

    [RelayCommand]
    private void ToggleQueue() => IsQueueOpen = !IsQueueOpen;

    [RelayCommand]
    private void StartSleep(string key)
    {
        switch (key)
        {
            case "off": _sleepTimer.Cancel(); break;
            case "end": _sleepTimer.Start(SleepTimerMode.EndOfTrack); break;
            case "15": _sleepTimer.Start(SleepTimerMode.Minutes15); break;
            case "30": _sleepTimer.Start(SleepTimerMode.Minutes30); break;
            case "45": _sleepTimer.Start(SleepTimerMode.Minutes45); break;
            case "60": _sleepTimer.Start(SleepTimerMode.Minutes60); break;
        }
        OnPropertyChanged(nameof(SleepLabel));
    }

    [RelayCommand]
    private async Task LikeAsync()
    {
        if (CurrentTrack is null) return;
        await _client.SetLikeAsync(CurrentTrack.VideoId, LikeStatus.Like).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DislikeAsync()
    {
        if (CurrentTrack is null) return;
        await _client.SetLikeAsync(CurrentTrack.VideoId, LikeStatus.Dislike).ConfigureAwait(true);
    }

    public Task PlayAsync(Track track, IReadOnlyList<Track>? queue = null)
        => _engine.PlayTrackAsync(track, queue);

    public void MoveQueue(int from, int to) => _engine.MoveQueueItem(from, to);
}

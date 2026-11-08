using YtLight.Core.Abstractions;
using YtLight.Core.Models;

namespace YtLight.Playback;

public sealed class AudioEngine : IAudioEngine
{
    private readonly IAudioPlayerBackend _playerBackend;
    private readonly IInnerTubeClient _client;
    private readonly IPlaybackHistoryReporter? _historyReporter;
    private readonly List<QueueItem> _queue = [];
    private readonly List<int> _shuffleOrder = [];
    private readonly Timer _progressTimer;

    private int _index;
    private double _volume = 0.85;
    private bool _muted;
    private CancellationTokenSource? _fadeCts;
    private DateTimeOffset _lastHistoryReportTime = DateTimeOffset.MinValue;
    private bool _isAdvancing;

    public AudioEngine(
        IAudioPlayerBackend playerBackend,
        IInnerTubeClient client,
        IPlaybackHistoryReporter? historyReporter = null)
    {
        _playerBackend = playerBackend;
        _client = client;
        _historyReporter = historyReporter;

        ApplyVolumeInternal();

        // 250ms periodic timer for UI seekbar & watchtime reports
        _progressTimer = new Timer(OnTimerTick, null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
    }

    public bool IsPlaying { get; private set; }
    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; private set; }
    public RepeatMode RepeatMode { get; set; }
    public Track? CurrentTrack { get; private set; }
    public IReadOnlyList<QueueItem> Queue => _queue;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            ApplyVolumeInternal();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            ApplyVolumeInternal();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool Shuffle
    {
        get => _shuffleOrder.Count > 0;
        set
        {
            _shuffleOrder.Clear();
            if (value && _queue.Count > 0)
            {
                _shuffleOrder.AddRange(Enumerable.Range(0, _queue.Count));
                for (var i = _shuffleOrder.Count - 1; i > 0; i--)
                {
                    var j = Random.Shared.Next(i + 1);
                    (_shuffleOrder[i], _shuffleOrder[j]) = (_shuffleOrder[j], _shuffleOrder[i]);
                }
            }

            QueueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? StateChanged;
    public event EventHandler? TrackChanged;
    public event EventHandler? QueueChanged;
    public event EventHandler? TrackFinished;
    public event EventHandler? PlaybackEnded;

    public async Task PlayTrackAsync(Track track, IReadOnlyList<Track>? queue = null, CancellationToken cancellationToken = default)
    {
        _queue.Clear();
        var source = queue is { Count: > 0 } ? queue : [track];
        foreach (var item in source)
            _queue.Add(new QueueItem { Track = item });

        _index = Math.Max(0, _queue.FindIndex(q => q.Track.VideoId == track.VideoId));
        if (_index < 0) _index = 0;
        CurrentTrack = _queue[_index].Track;
        Position = TimeSpan.Zero;
        Duration = CurrentTrack.Duration;

        if (_historyReporter != null)
            await _historyReporter.StopAsync().ConfigureAwait(false);

        TrackChanged?.Invoke(this, EventArgs.Empty);
        QueueChanged?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            await _playerBackend.PlayVideoAsync(track.VideoId, cancellationToken).ConfigureAwait(false);
            ApplyVolumeInternal();
            IsPlaying = true;
            _lastHistoryReportTime = DateTimeOffset.UtcNow;
        }
        catch
        {
            IsPlaying = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Shuffle)
            Shuffle = true;

        TrackChanged?.Invoke(this, EventArgs.Empty);
        QueueChanged?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task PlayPauseAsync()
    {
        if (CurrentTrack is null)
        {
            if (_queue.Count > 0)
                await PlayTrackAsync(_queue[0].Track, _queue.Select(q => q.Track).ToList()).ConfigureAwait(false);
            return;
        }

        await _playerBackend.PlayPauseAsync().ConfigureAwait(false);
        IsPlaying = !IsPlaying;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task NextAsync()
    {
        if (_queue.Count == 0)
            return Task.CompletedTask;

        if (RepeatMode == RepeatMode.One && CurrentTrack != null)
        {
            TrackFinished?.Invoke(this, EventArgs.Empty);
            Position = TimeSpan.Zero;
            return PlayTrackAsync(CurrentTrack, _queue.Select(q => q.Track).ToList());
        }

        var next = ResolveNextIndex();
        if (next is null)
        {
            TrackFinished?.Invoke(this, EventArgs.Empty);
            IsPlaying = false;
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        TrackFinished?.Invoke(this, EventArgs.Empty);
        _index = next.Value;
        CurrentTrack = _queue[_index].Track;
        Duration = CurrentTrack.Duration;
        Position = TimeSpan.Zero;
        return PlayTrackAsync(CurrentTrack, _queue.Select(q => q.Track).ToList());
    }

    public Task PreviousAsync()
    {
        if (_queue.Count == 0)
            return Task.CompletedTask;

        if (Position > TimeSpan.FromSeconds(3))
        {
            Seek(TimeSpan.Zero);
            Position = TimeSpan.Zero;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        _index = _index == 0 ? (RepeatMode == RepeatMode.All ? _queue.Count - 1 : 0) : _index - 1;
        CurrentTrack = _queue[_index].Track;
        Duration = CurrentTrack.Duration;
        Position = TimeSpan.Zero;
        return PlayTrackAsync(CurrentTrack, _queue.Select(q => q.Track).ToList());
    }

    public void Seek(TimeSpan position)
    {
        if (Duration <= TimeSpan.Zero)
            return;

        Position = position < TimeSpan.Zero ? TimeSpan.Zero : (position > Duration ? Duration : position);
        _ = _playerBackend.SeekAsync(Position);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MoveQueueItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _queue.Count || toIndex < 0 || toIndex >= _queue.Count)
            return;
        var item = _queue[fromIndex];
        _queue.RemoveAt(fromIndex);
        _queue.Insert(toIndex, item);
        if (_index == fromIndex) _index = toIndex;
        else if (fromIndex < _index && toIndex >= _index) _index--;
        else if (fromIndex > _index && toIndex <= _index) _index++;
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FadeOutAndStop(TimeSpan fadeDuration)
    {
        _fadeCts?.Cancel();
        _fadeCts = new CancellationTokenSource();
        _ = FadeAsync(fadeDuration, _fadeCts.Token);
    }

    private void ApplyVolumeInternal()
    {
        // Logarithmic volume curve for natural hearing perception
        var logVol = LoudnessNormalizer.ToLogVolume(_volume);
        _ = _playerBackend.SetVolumeAsync(logVol);
        _ = _playerBackend.SetMutedAsync(_muted);
    }

    private void OnTimerTick(object? state)
    {
        try
        {
            var (isPlaying, cur, dur, ended) = _playerBackend.QueryState();

            if (cur > TimeSpan.Zero)
                Position = cur;
            if (dur > TimeSpan.Zero)
                Duration = dur;

            IsPlaying = isPlaying;
            StateChanged?.Invoke(this, EventArgs.Empty);

            if (ended && !_isAdvancing)
            {
                _isAdvancing = true;
                _ = Task.Run(async () =>
                {
                    try { await NextAsync().ConfigureAwait(false); }
                    finally { _isAdvancing = false; }
                });
                return;
            }

            // Report watchtime every 10 seconds
            if (_historyReporter != null && (DateTimeOffset.UtcNow - _lastHistoryReportTime) > TimeSpan.FromSeconds(10))
            {
                _lastHistoryReportTime = DateTimeOffset.UtcNow;
                _ = _historyReporter.ReportWatchtimeAsync(Position, Duration, !IsPlaying);
            }
        }
        catch
        {
            // Ignore query race
        }
    }

    private async Task FadeAsync(TimeSpan fadeDuration, CancellationToken token)
    {
        var start = Volume;
        var steps = Math.Max(4, (int)(fadeDuration.TotalMilliseconds / 120));
        try
        {
            for (var i = 0; i < steps; i++)
            {
                Volume = start * (1.0 - (i + 1) / (double)steps);
                await Task.Delay((int)(fadeDuration.TotalMilliseconds / steps), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        IsPlaying = false;
        await _playerBackend.PlayPauseAsync().ConfigureAwait(false);
        Volume = start;
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private int? ResolveNextIndex()
    {
        if (Shuffle && _shuffleOrder.Count == _queue.Count)
        {
            var pos = _shuffleOrder.IndexOf(_index);
            if (pos >= 0 && pos + 1 < _shuffleOrder.Count)
                return _shuffleOrder[pos + 1];
            return RepeatMode == RepeatMode.All ? _shuffleOrder[0] : null;
        }

        if (_index + 1 < _queue.Count)
            return _index + 1;
        return RepeatMode == RepeatMode.All ? 0 : null;
    }

    public void Dispose()
    {
        _progressTimer.Dispose();
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _playerBackend.Dispose();
    }
}

using YtLight.Core.Models;

namespace YtLight.Core.Abstractions;

public interface IAudioEngine : IDisposable
{
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    bool IsMuted { get; set; }
    RepeatMode RepeatMode { get; set; }
    bool Shuffle { get; set; }

    event EventHandler? StateChanged;
    event EventHandler? TrackChanged;
    event EventHandler? QueueChanged;
    event EventHandler? TrackFinished;
    event EventHandler? PlaybackEnded;

    Track? CurrentTrack { get; }
    IReadOnlyList<QueueItem> Queue { get; }

    Task PlayTrackAsync(Track track, IReadOnlyList<Track>? queue = null, CancellationToken cancellationToken = default);
    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
    void Seek(TimeSpan position);
    void MoveQueueItem(int fromIndex, int toIndex);
    void FadeOutAndStop(TimeSpan fadeDuration);
}

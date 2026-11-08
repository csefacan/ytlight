namespace YtLight.Core.Abstractions;

public interface IAudioPlayerBackend : IDisposable
{
    Task InitializeAsync();
    Task PlayVideoAsync(string videoId, CancellationToken cancellationToken = default);
    Task PlayPauseAsync();
    Task SeekAsync(TimeSpan position);
    Task SetVolumeAsync(double volume);
    Task SetMutedAsync(bool isMuted);
    (bool isPlaying, TimeSpan position, TimeSpan duration, bool ended) QueryState();
}

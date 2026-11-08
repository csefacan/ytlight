namespace YtLight.Core.Models;

public sealed class PlayerPayload
{
    public required Track Track { get; init; }
    public IReadOnlyList<AudioStreamInfo> AudioStreams { get; init; } = [];
    public string? VideostatsPlaybackUrl { get; init; }
    public string? VideostatsWatchtimeUrl { get; init; }
    public string? VideostatsDelayplayUrl { get; init; }
    public double? LoudnessDb { get; init; }
    public int DurationMs { get; init; }
    public string? PlaybackTrackingWatchUrl { get; init; }
}

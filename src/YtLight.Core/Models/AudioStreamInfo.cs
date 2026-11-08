namespace YtLight.Core.Models;

public sealed record AudioStreamInfo
{
    public required int Itag { get; init; }
    public required string MimeType { get; init; }
    public required int Bitrate { get; init; }
    public int? AverageBitrate { get; init; }
    public string? Url { get; init; }
    public string? SignatureCipher { get; init; }
    public string? AudioQuality { get; init; }
    public string? AudioSampleRate { get; init; }
    public int? AudioChannels { get; init; }
    public long? ContentLength { get; init; }
    public bool IsOpus => MimeType.Contains("opus", StringComparison.OrdinalIgnoreCase)
                          || MimeType.Contains("webm", StringComparison.OrdinalIgnoreCase);
    public bool IsAac => MimeType.Contains("mp4a", StringComparison.OrdinalIgnoreCase)
                         || MimeType.Contains("mp4", StringComparison.OrdinalIgnoreCase);
}

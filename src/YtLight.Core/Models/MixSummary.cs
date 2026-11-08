namespace YtLight.Core.Models;

public sealed class MixSummary
{
    public required string BrowseId { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = "";
    public string? ThumbnailUrl { get; init; }
    public string? PlaylistId { get; init; }
}

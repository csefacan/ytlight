namespace YtLight.Core.Models;

public sealed class AlbumSummary
{
    public required string BrowseId { get; init; }
    public required string Title { get; init; }
    public string Artists { get; init; } = "";
    public string? Year { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? PlaylistId { get; init; }
}

namespace YtLight.Core.Models;

public sealed class HomeSnapshot
{
    public IReadOnlyList<HomeShelf> Shelves { get; init; } = [];
}

public sealed class HomeShelf
{
    public required string Title { get; init; }
    public IReadOnlyList<Track> Tracks { get; init; } = [];
    public IReadOnlyList<MixSummary> Mixes { get; init; } = [];
    public IReadOnlyList<AlbumSummary> Albums { get; init; } = [];
}

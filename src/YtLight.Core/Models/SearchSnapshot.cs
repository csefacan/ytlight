namespace YtLight.Core.Models;

public sealed class SearchSnapshot
{
    public string Query { get; init; } = "";
    public IReadOnlyList<Track> Songs { get; init; } = [];
    public IReadOnlyList<AlbumSummary> Albums { get; init; } = [];
    public IReadOnlyList<PlaylistSummary> Playlists { get; init; } = [];
}

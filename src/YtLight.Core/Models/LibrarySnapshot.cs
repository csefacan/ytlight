namespace YtLight.Core.Models;

public sealed class LibrarySnapshot
{
    public IReadOnlyList<PlaylistSummary> Playlists { get; init; } = [];
    public IReadOnlyList<AlbumSummary> Albums { get; init; } = [];
    public IReadOnlyList<Track> Recents { get; init; } = [];
}

using System.ComponentModel;

namespace YtLight.Core.Models;

public sealed class PlaylistSummary : INotifyPropertyChanged
{
    private int _trackCount;

    public required string PlaylistId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }

    public int TrackCount
    {
        get => _trackCount;
        set
        {
            if (_trackCount != value)
            {
                _trackCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrackCount)));
            }
        }
    }

    public string? ThumbnailUrl { get; init; }
    public string? Author { get; init; }
    public bool IsLikedSongs { get; init; }
    public bool IsOwn { get; init; }

    public string BrowseId =>
        PlaylistId.StartsWith("VL", StringComparison.Ordinal) || PlaylistId.StartsWith("FE", StringComparison.Ordinal)
            ? PlaylistId
            : "VL" + PlaylistId;

    public event PropertyChangedEventHandler? PropertyChanged;
}

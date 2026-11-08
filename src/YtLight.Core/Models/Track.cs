namespace YtLight.Core.Models;

public sealed class Track
{
    public required string VideoId { get; init; }
    public required string Title { get; init; }
    public string Artists { get; init; } = "";
    public string? Album { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? PlaylistId { get; init; }
    public string? SetVideoId { get; init; }
    public LikeStatus LikeStatus { get; init; } = LikeStatus.Indifferent;
    public string? FeedbackToken { get; init; }

    public string DurationText =>
        Duration <= TimeSpan.Zero
            ? ""
            : Duration.TotalHours >= 1
                ? Duration.ToString(@"h\:mm\:ss")
                : Duration.ToString(@"m\:ss");
}

using YtLight.Core.Models;

namespace YtLight.Core.Abstractions;

public interface IPlaybackHistoryReporter
{
    Task StartAsync(PlayerPayload player, CancellationToken cancellationToken = default);
    Task ReportWatchtimeAsync(TimeSpan position, TimeSpan duration, bool paused, CancellationToken cancellationToken = default);
    Task StopAsync();
}

using YtLight.Core.Models;

namespace YtLight.Core.Abstractions;

public interface IProfileService
{
    YoutubeProfile? ActiveProfile { get; }
    IReadOnlyList<YoutubeProfile> Profiles { get; }
    event EventHandler? ActiveProfileChanged;

    Task<IReadOnlyList<YoutubeProfile>> RefreshAsync(CancellationToken cancellationToken = default);
    Task SelectAsync(YoutubeProfile profile, CancellationToken cancellationToken = default);
}

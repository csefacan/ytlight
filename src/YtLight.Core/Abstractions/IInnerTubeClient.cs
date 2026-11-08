using System.Text.Json;
using YtLight.Core.Models;

namespace YtLight.Core.Abstractions;

public interface IInnerTubeClient
{
    YoutubeProfile? ActiveProfile { get; }
    bool IsAuthenticated { get; }

    void SetSession(Session.AuthSession? session);
    void SetActiveProfile(YoutubeProfile? profile);

    Task BootstrapAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YoutubeProfile>> ListAccountsAsync(CancellationToken cancellationToken = default);
    Task<LibrarySnapshot> GetLibraryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(string playlistId, CancellationToken cancellationToken = default);
    Task<HomeSnapshot> GetHomeAsync(CancellationToken cancellationToken = default);
    Task<HomeSnapshot> GetExploreAsync(CancellationToken cancellationToken = default);
    Task<SearchSnapshot> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<PlayerPayload> GetPlayerAsync(string videoId, CancellationToken cancellationToken = default);
    Task<PlayerPayload> GetAndroidPlayerAsync(string videoId, CancellationToken cancellationToken = default);
    Task SetLikeAsync(string videoId, LikeStatus status, CancellationToken cancellationToken = default);
    Task<string> CreatePlaylistAsync(string title, IEnumerable<string>? videoIds = null, CancellationToken cancellationToken = default);
    Task AddToPlaylistAsync(string playlistId, string videoId, CancellationToken cancellationToken = default);
    Task RemoveFromPlaylistAsync(string playlistId, string videoId, string? setVideoId, CancellationToken cancellationToken = default);
    Task<JsonDocument> PostRawAsync(string endpoint, IReadOnlyDictionary<string, object?> payload, CancellationToken cancellationToken = default);
}

using YtLight.Core.Session;

namespace YtLight.Core.Abstractions;

public interface ISessionStore
{
    Task<AuthSession?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

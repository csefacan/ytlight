using System.Security.Cryptography;
using System.Text.Json;
using YtLight.Core.Abstractions;
using YtLight.Core.Session;

namespace YtLight.InnerTube.Auth;

public sealed class DpapiSessionStore : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _path;

    public DpapiSessionStore(string? path = null)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YtLight");
        Directory.CreateDirectory(root);
        _path = path ?? Path.Combine(root, "session.dpapi");
    }

    public async Task<AuthSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
            return null;

        var protectedBytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        var jsonBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var session = JsonSerializer.Deserialize<AuthSession>(jsonBytes, JsonOptions);
        return session is { IsAuthenticated: true } ? session : null;
    }

    public async Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions);
        var protectedBytes = ProtectedData.Protect(json, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var tmp = _path + ".tmp";
        await File.WriteAllBytesAsync(tmp, protectedBytes, cancellationToken).ConfigureAwait(false);
        File.Copy(tmp, _path, overwrite: true);
        File.Delete(tmp);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_path))
            File.Delete(_path);
        return Task.CompletedTask;
    }
}

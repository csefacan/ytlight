using System.Security.Cryptography;
using System.Text;

namespace YtLight.InnerTube.Auth;

public static class SapisidHash
{
    public static string BuildAuthorizationHeader(
        string? sapisid,
        string? papisid1,
        string? papisid3,
        string origin = InnerTubeOptions.DefaultOrigin)
    {
        // YouTube InnerTube strictly expects a single SAPISIDHASH header token
        var secret = !string.IsNullOrWhiteSpace(sapisid)
            ? sapisid
            : !string.IsNullOrWhiteSpace(papisid3)
                ? papisid3
                : papisid1;

        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("SAPISID cookie is missing; the session is not authenticated.");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = Hash(timestamp, secret, origin);
        return $"SAPISIDHASH {timestamp}_{hash}";
    }

    private static string Hash(long timestamp, string sapisid, string origin)
    {
        var input = $"{timestamp} {sapisid} {origin}";
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

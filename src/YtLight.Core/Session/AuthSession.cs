using YtLight.Core.Models;

namespace YtLight.Core.Session;

public sealed class AuthSession
{
    public IReadOnlyList<StoredCookie> Cookies { get; init; } = [];
    public string? VisitorData { get; init; }
    public YoutubeProfile? ActiveProfile { get; init; }
    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IsAuthenticated =>
        Cookies.Any(c => c.Name is "SAPISID" or "__Secure-3PAPISID")
        && Cookies.Any(c => c.Name is "SID" or "__Secure-3PSID" or "__Secure-1PSID");

    public string ToCookieHeader(string domainFilter = "youtube.com")
    {
        // YouTube requests must send cookies that match youtube.com.
        // Sending accounts.google.com or foreign domain cookies to music.youtube.com causes YouTube to treat the session as anonymous.
        var matching = Cookies
            .Where(c => string.IsNullOrEmpty(domainFilter) || c.Domain.EndsWith(domainFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var source = matching.Count > 0 ? matching : Cookies;

        var unique = source
            .GroupBy(c => c.Name, StringComparer.Ordinal)
            .Select(g => g.Last());

        return string.Join("; ", unique.Select(c => $"{c.Name}={c.Value}"));
    }

    public string? GetCookie(string name, string? domainFilter = "youtube.com")
    {
        if (!string.IsNullOrEmpty(domainFilter))
        {
            var match = Cookies
                .Where(c => c.Domain.EndsWith(domainFilter, StringComparison.OrdinalIgnoreCase))
                .LastOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
            if (match is not null)
                return match.Value;
        }

        return Cookies.LastOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal))?.Value;
    }

    public string? Sapisid => GetCookie("SAPISID") ?? GetCookie("__Secure-3PAPISID");

    public string? Secure3Papisid => GetCookie("__Secure-3PAPISID");

    public string? Secure1Papisid => GetCookie("__Secure-1PAPISID");

    public AuthSession WithProfile(YoutubeProfile? profile) => new()
    {
        Cookies = Cookies,
        VisitorData = VisitorData,
        ActiveProfile = profile,
        SavedAt = DateTimeOffset.UtcNow
    };

    public AuthSession WithVisitorData(string? visitorData) => new()
    {
        Cookies = Cookies,
        VisitorData = visitorData ?? VisitorData,
        ActiveProfile = ActiveProfile,
        SavedAt = SavedAt
    };
}

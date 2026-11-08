namespace YtLight.Core.Session;

public sealed class StoredCookie
{
    public required string Name { get; init; }
    public required string Value { get; init; }
    public string Domain { get; init; } = ".youtube.com";
    public string Path { get; init; } = "/";
    public bool Secure { get; init; }
    public bool HttpOnly { get; init; }
}

namespace YtLight.InnerTube;

public sealed class ClientOverride
{
    public required string ApiOrigin { get; init; }
    public required string ApiKey { get; init; }
    public required string ClientName { get; init; }
    public required int ClientNameId { get; init; }
    public required string ClientVersion { get; init; }
    public required string UserAgent { get; init; }
    public IReadOnlyDictionary<string, object?> ExtraClientFields { get; init; } = new Dictionary<string, object?>();

    public static ClientOverride WebRemix(InnerTubeOptions options) => new()
    {
        ApiOrigin = options.Origin,
        ApiKey = options.ApiKey,
        ClientName = "WEB_REMIX",
        ClientNameId = InnerTubeOptions.WebRemixClientNameId,
        ClientVersion = options.ClientVersion,
        UserAgent = options.UserAgent,
        ExtraClientFields = new Dictionary<string, object?>
        {
            ["osName"] = "Windows",
            ["osVersion"] = "10.0",
            ["platform"] = "DESKTOP"
        }
    };

    public static ClientOverride AndroidMusic(InnerTubeOptions options) => new()
    {
        ApiOrigin = "https://youtubei.googleapis.com",
        ApiKey = InnerTubeOptions.AndroidMusicApiKey,
        ClientName = "ANDROID_MUSIC",
        ClientNameId = InnerTubeOptions.AndroidMusicClientNameId,
        ClientVersion = InnerTubeOptions.AndroidMusicVersion,
        UserAgent = InnerTubeOptions.AndroidMusicUserAgent,
        ExtraClientFields = new Dictionary<string, object?>
        {
            ["androidSdkVersion"] = 34,
            ["osName"] = "Android",
            ["osVersion"] = "14",
            ["platform"] = "MOBILE"
        }
    };
}

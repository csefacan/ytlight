using System.Text.Json;
using YtLight.Core.Models;
using YtLight.InnerTube;

namespace YtLight.InnerTube;

public static class ClientPresets
{
    public static ClientOverride Ios(InnerTubeOptions options) => new()
    {
        ApiOrigin = "https://youtubei.googleapis.com",
        ApiKey = "AIzaSyAOghZGza2MQSZkY_zfZ370N-PUdXEjCPw",
        ClientName = "IOS",
        ClientNameId = 5,
        ClientVersion = "19.29.1",
        UserAgent = "com.google.ios.youtube/19.29.1 (iPhone16,2; U; CPU iOS 17_5_1 like Mac OS X; en_US)",
        ExtraClientFields = new Dictionary<string, object?>
        {
            ["deviceMake"] = "Apple",
            ["deviceModel"] = "iPhone16,2",
            ["osName"] = "iOS",
            ["osVersion"] = "17.5.1.21F90",
            ["platform"] = "MOBILE"
        }
    };

    public static ClientOverride TvEmbedded(InnerTubeOptions options) => new()
    {
        ApiOrigin = "https://www.youtube.com",
        ApiKey = options.ApiKey,
        ClientName = "TVHTML5_SIMPLY_EMBEDDED_PLAYER",
        ClientNameId = 85,
        ClientVersion = "2.0",
        UserAgent = "Mozilla/5.0 (PlayStation; PlayStation 4/10.01) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/10.0 Safari/605.1.15",
        ExtraClientFields = new Dictionary<string, object?>
        {
            ["clientScreen"] = "EMBED",
            ["platform"] = "TV"
        }
    };
}

namespace YtLight.InnerTube;

public sealed class InnerTubeOptions
{
    public const string DefaultOrigin = "https://music.youtube.com";
    public const int WebRemixClientNameId = 67;
    public const int AndroidMusicClientNameId = 21;
    public const string AndroidMusicApiKey = "AIzaSyAOghZGza2MQSZkY_zfZ370N-PUdXEjCPw";
    public const string AndroidMusicVersion = "7.27.52";
    public const string AndroidMusicUserAgent =
        "com.google.android.apps.youtube.music/7.27.52 (Linux; U; Android 14; en_US)";

    public string Origin { get; set; } = DefaultOrigin;
    public string ClientName { get; set; } = "WEB_REMIX";
    public string ClientVersion { get; set; } = "1.20241016.01.00";
    public string ApiKey { get; set; } = "AIzaSyC9XL3ZjWddXya6X74dJoCTL-WEYFDNX30";
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
    public string Hl { get; set; } = "tr";
    public string Gl { get; set; } = "TR";
    public int? SignatureTimestamp { get; set; }
    public string? PlayerJsUrl { get; set; }
}

using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using YtLight.Core.Session;

namespace YtLight.InnerTube;

public sealed class YoutubeMusicBootstrapper
{
    private static readonly Regex ApiKeyRx = new("""INNERTUBE_API_KEY":"([^"]+)""", RegexOptions.Compiled);
    private static readonly Regex ClientVersionRx = new("""INNERTUBE_CLIENT_VERSION":"([^"]+)""", RegexOptions.Compiled);
    private static readonly Regex VisitorRx = new("""VISITOR_DATA":"([^"]+)""", RegexOptions.Compiled);
    private static readonly Regex StsRx = new("""(?:SIGNATURE_TIMESTAMP|sts)":\s*"?(\d+)""", RegexOptions.Compiled);
    private static readonly Regex PlayerJsRx = new("""jsUrl":"([^"]+base\.js)""", RegexOptions.Compiled);
    private static readonly Regex PlayerJsUrlRx = new("""PLAYER_JS_URL":"([^"]+)""", RegexOptions.Compiled);

    private readonly HttpClient _http;

    public YoutubeMusicBootstrapper(HttpClient http) => _http = http;

    public async Task ApplyAsync(InnerTubeOptions options, AuthSession? session, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, options.Origin + "/?hl=" + options.Hl);
        request.Headers.TryAddWithoutValidation("User-Agent", options.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        if (session is not null)
            request.Headers.TryAddWithoutValidation("Cookie", session.ToCookieHeader());

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(html))
            return;

        var apiKey = ApiKeyRx.Match(html);
        if (apiKey.Success)
            options.ApiKey = apiKey.Groups[1].Value;

        var version = ClientVersionRx.Match(html);
        if (version.Success)
            options.ClientVersion = version.Groups[1].Value;

        var visitor = VisitorRx.Match(html);

        var sts = StsRx.Match(html);
        if (sts.Success && int.TryParse(sts.Groups[1].Value, out var timestamp))
            options.SignatureTimestamp = timestamp;

        var js = PlayerJsRx.Match(html);
        if (!js.Success)
            js = PlayerJsUrlRx.Match(html);
        if (js.Success)
        {
            var url = js.Groups[1].Value.Replace("\\u0026", "&", StringComparison.Ordinal).Replace("\\/", "/", StringComparison.Ordinal);
            options.PlayerJsUrl = url.StartsWith("http", StringComparison.Ordinal)
                ? url
                : "https://www.youtube.com" + url;
        }

        if (visitor.Success)
            LastVisitorData = visitor.Groups[1].Value;
    }

    public string? LastVisitorData { get; private set; }
}

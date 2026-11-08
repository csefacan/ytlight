namespace YtLight.InnerTube;

public sealed class InnerTubeException : Exception
{
    public int StatusCode { get; }
    public string Endpoint { get; }
    public string? ResponseBody { get; }

    public InnerTubeException(string endpoint, int statusCode, string message, string? responseBody = null)
        : base(message)
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

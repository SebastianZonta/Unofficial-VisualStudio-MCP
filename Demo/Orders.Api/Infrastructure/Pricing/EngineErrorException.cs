public sealed class EngineErrorException(int statusCode, string body) : Exception($"Pricing engine returned {statusCode}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}

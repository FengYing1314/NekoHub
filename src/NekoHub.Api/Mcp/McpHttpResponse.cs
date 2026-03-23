namespace NekoHub.Api.Mcp;

public sealed record McpHttpResponse(
    int StatusCode,
    object? Body = null,
    IReadOnlyDictionary<string, string>? Headers = null);

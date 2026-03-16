using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Resources;

public interface IMcpResource
{
    Task<IReadOnlyList<McpResourceDescriptor>> ListAsync(CancellationToken cancellationToken);

    bool CanHandle(Uri resourceUri);

    Task<McpResourceReadResult> ReadAsync(Uri resourceUri, CancellationToken cancellationToken);
}

public sealed record McpResourceReadResult(string Uri, object StructuredContent);

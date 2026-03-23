using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Tools;

public interface IMcpTool
{
    McpToolDescriptor Definition { get; }

    Task<McpToolInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken);
}

public sealed record McpToolInvocationResult(object StructuredContent);

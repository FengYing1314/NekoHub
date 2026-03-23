using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Prompts;

public interface IMcpPrompt
{
    McpPromptDescriptor Definition { get; }

    Task<McpPromptInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken);
}

public sealed record McpPromptInvocationResult(
    string Name,
    string? Description,
    IReadOnlyList<McpPromptMessage> Messages);

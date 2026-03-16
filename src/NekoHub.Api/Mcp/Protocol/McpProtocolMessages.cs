using System.Text.Json;
using System.Text.Json.Serialization;

namespace NekoHub.Api.Mcp.Protocol;

public sealed record McpJsonRpcSuccessResponse(JsonElement Id, object Result)
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = McpProtocolConstants.JsonRpcVersion;
}

public sealed record McpJsonRpcErrorResponse(JsonElement? Id, McpJsonRpcError Error)
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = McpProtocolConstants.JsonRpcVersion;
}

public sealed record McpJsonRpcError(int Code, string Message, object? Data = null);

public sealed record McpInitializeResult(
    string ProtocolVersion,
    McpServerCapabilities Capabilities,
    McpImplementation ServerInfo)
{
    public string? Instructions { get; init; }
}

public sealed record McpServerCapabilities
{
    public McpToolsCapability? Tools { get; init; }

    public McpResourcesCapability? Resources { get; init; }

    public McpPromptsCapability? Prompts { get; init; }
}

public sealed record McpToolsCapability
{
    public bool? ListChanged { get; init; }
}

public sealed record McpResourcesCapability
{
    public bool? Subscribe { get; init; }

    public bool? ListChanged { get; init; }
}

public sealed record McpPromptsCapability
{
    public bool? ListChanged { get; init; }
}

public sealed record McpImplementation(string Name, string Version)
{
    public string? Title { get; init; }
}

public sealed record McpListToolsResult(IReadOnlyList<McpToolDescriptor> Tools)
{
    public string? NextCursor { get; init; }
}

public sealed record McpListResourcesResult(IReadOnlyList<McpResourceDescriptor> Resources)
{
    public string? NextCursor { get; init; }
}

public sealed record McpListPromptsResult(IReadOnlyList<McpPromptDescriptor> Prompts)
{
    public string? NextCursor { get; init; }
}

public sealed record McpToolDescriptor(string Name, object InputSchema)
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public object? OutputSchema { get; init; }

    public McpToolAnnotations? Annotations { get; init; }
}

public sealed record McpToolAnnotations
{
    public bool? ReadOnlyHint { get; init; }

    public bool? DestructiveHint { get; init; }

    public bool? IdempotentHint { get; init; }

    public bool? OpenWorldHint { get; init; }
}

public sealed record McpResourceDescriptor(string Uri, string Name)
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? MimeType { get; init; }
}

public sealed record McpPromptDescriptor(string Name)
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<McpPromptArgumentDescriptor>? Arguments { get; init; }
}

public sealed record McpPromptArgumentDescriptor(string Name)
{
    public string? Description { get; init; }

    public bool? Required { get; init; }
}

public sealed record McpCallToolResult(IReadOnlyList<McpTextContent> Content)
{
    public object? StructuredContent { get; init; }

    public bool? IsError { get; init; }
}

public sealed record McpTextContent(string Text)
{
    public string Type { get; init; } = "text";
}

public sealed record McpReadResourceResult(IReadOnlyList<McpTextResourceContent> Contents);

public sealed record McpTextResourceContent(string Uri, string MimeType, string Text)
{
    public string Type { get; init; } = "text";
}

public sealed record McpGetPromptResult(string Name, IReadOnlyList<McpPromptMessage> Messages)
{
    public string? Description { get; init; }
}

public sealed record McpPromptMessage(string Role, McpPromptTextContent Content);

public sealed record McpPromptTextContent(string Text)
{
    public string Type { get; init; } = "text";
}

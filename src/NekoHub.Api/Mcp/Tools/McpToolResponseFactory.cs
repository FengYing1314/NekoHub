using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Tools;

internal static class McpToolResponseFactory
{
    public static McpCallToolResult Success(object structuredContent)
    {
        return new McpCallToolResult([new McpTextContent(JsonSerializer.Serialize(structuredContent, McpJsonOptions.Default))])
        {
            StructuredContent = structuredContent
        };
    }

    public static McpCallToolResult Error(string code, string message)
    {
        var structuredError = new
        {
            error = new
            {
                code,
                message
            }
        };

        return new McpCallToolResult([new McpTextContent(JsonSerializer.Serialize(structuredError, McpJsonOptions.Default))])
        {
            StructuredContent = structuredError,
            IsError = true
        };
    }
}

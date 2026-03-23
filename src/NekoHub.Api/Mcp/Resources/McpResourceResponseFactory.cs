using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Resources;

internal static class McpResourceResponseFactory
{
    public static McpReadResourceResult Success(McpResourceReadResult readResult)
    {
        var text = JsonSerializer.Serialize(readResult.StructuredContent, McpJsonOptions.Default);
        return new McpReadResourceResult(
        [
            new McpTextResourceContent(
                Uri: readResult.Uri,
                MimeType: "application/json",
                Text: text)
        ]);
    }
}

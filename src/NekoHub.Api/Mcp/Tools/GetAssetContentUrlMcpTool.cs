using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Tools;

public sealed class GetAssetContentUrlMcpTool(IAssetContentService assetContentService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "get_asset_content_url",
        InputSchema: new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["id"] = new
                {
                    type = "string",
                    format = "uuid"
                }
            },
            required = new[] { "id" },
            additionalProperties = false
        })
    {
        Title = "Get Asset Content URL",
        Description = "Return the resolved public content URL for an asset without exposing storage internals.",
        OutputSchema = McpAssetToolSchemas.AssetContentUrl,
        Annotations = new McpToolAnnotations
        {
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = false
        }
    };

    public async Task<McpToolInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        var input = McpToolArgumentParser.ParseRequired<Arguments>(arguments, Definition.Name);
        var assetId = ParseAssetId(input.Id);
        var redirect = await assetContentService.GetRedirectAsync(assetId, cancellationToken);
        return new McpToolInvocationResult(McpAssetToolModelMapper.ToView(redirect));
    }

    private static Guid ParseAssetId(string? id)
    {
        if (!Guid.TryParse(id, out var assetId))
        {
            throw new ValidationException("tool_argument_invalid", "Argument 'id' must be a valid GUID.");
        }

        return assetId;
    }

    private sealed record Arguments(string? Id);
}

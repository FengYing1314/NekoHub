using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Tools;

public sealed class GetAssetMcpTool(IAssetQueryService assetQueryService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "get_asset",
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
        Title = "Get Asset",
        Description = "Return the asset detail read model by asset id, including derivatives and structured results.",
        OutputSchema = McpAssetToolSchemas.AssetDetail,
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
        var asset = await assetQueryService.GetByIdAsync(assetId, cancellationToken);
        return new McpToolInvocationResult(McpAssetToolModelMapper.ToView(asset));
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

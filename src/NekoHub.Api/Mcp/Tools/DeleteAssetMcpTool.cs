using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Commands;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Tools;

public sealed class DeleteAssetMcpTool(IAssetCommandService assetCommandService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "delete_asset",
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
        Title = "Delete Asset",
        Description = "Delete an asset and its derived artifacts through the existing application command service.",
        OutputSchema = McpAssetToolSchemas.DeleteAsset,
        Annotations = new McpToolAnnotations
        {
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false,
            OpenWorldHint = false
        }
    };

    public async Task<McpToolInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        var input = McpToolArgumentParser.ParseRequired<Arguments>(arguments, Definition.Name);
        var assetId = ParseAssetId(input.Id);
        var deleted = await assetCommandService.DeleteAsync(new DeleteAssetCommand(assetId), cancellationToken);
        return new McpToolInvocationResult(McpAssetToolModelMapper.ToView(deleted));
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

using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Commands;
using NekoHub.Application.Assets.Services;

namespace NekoHub.Api.Mcp.Tools;

public sealed class BatchDeleteAssetsMcpTool(IAssetCommandService assetCommandService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "batch_delete_assets",
        InputSchema: new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["ids"] = new
                {
                    type = "array",
                    items = new
                    {
                        type = "string",
                        format = "uuid"
                    }
                }
            },
            required = new[] { "ids" },
            additionalProperties = false
        })
    {
        Title = "Batch Delete Assets",
        Description = "Delete assets by reusing the existing single-asset deletion semantics for each id.",
        OutputSchema = McpAssetToolSchemas.BatchDeleteAssets,
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
        var ids = input.Ids ?? [];
        var parsedIds = ids
            .Select(static id => Guid.TryParse(id, out var parsedId) ? parsedId : (Guid?)null)
            .ToList();

        if (parsedIds.Any(static id => !id.HasValue))
        {
            throw new NekoHub.Application.Common.Exceptions.ValidationException(
                "tool_argument_invalid",
                "Argument 'ids' must contain only valid GUID values.");
        }

        var result = await assetCommandService.BatchDeleteAsync(
            new BatchDeleteAssetsCommand(parsedIds.Select(static id => id!.Value).ToList()),
            cancellationToken);

        return new McpToolInvocationResult(McpAssetToolModelMapper.ToView(result));
    }

    private sealed record Arguments(IReadOnlyList<string>? Ids);
}

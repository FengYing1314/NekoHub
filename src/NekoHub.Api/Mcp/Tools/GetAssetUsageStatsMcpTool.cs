using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Services;

namespace NekoHub.Api.Mcp.Tools;

public sealed class GetAssetUsageStatsMcpTool(IAssetQueryService assetQueryService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "get_asset_usage_stats",
        InputSchema: new
        {
            type = "object",
            properties = new Dictionary<string, object>(),
            additionalProperties = false
        })
    {
        Title = "Get Asset Usage Stats",
        Description = "Return current usage stats for active assets and their related derivatives and skill runs.",
        OutputSchema = McpAssetToolSchemas.AssetUsageStats,
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
        _ = McpToolArgumentParser.ParseOptional<NoArguments>(arguments, Definition.Name);
        var stats = await assetQueryService.GetUsageStatsAsync(cancellationToken);
        return new McpToolInvocationResult(McpAssetToolModelMapper.ToView(stats));
    }

    private sealed record NoArguments;
}

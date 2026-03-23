using System.Text.Json;
using Microsoft.Extensions.Options;
using NekoHub.Api.Configuration;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Queries;
using NekoHub.Application.Assets.Services;

namespace NekoHub.Api.Mcp.Tools;

public sealed class ListAssetsMcpTool(
    IAssetQueryService assetQueryService,
    IOptions<AssetApiOptions> assetApiOptions) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "list_assets",
        InputSchema: new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["page"] = new
                {
                    type = "integer",
                    minimum = 1
                },
                ["pageSize"] = new
                {
                    type = "integer",
                    minimum = 1
                }
            },
            additionalProperties = false
        })
    {
        Title = "List Assets",
        Description = "Return a paged list of assets using the existing asset list read model.",
        OutputSchema = McpAssetToolSchemas.AssetPage,
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
        var input = McpToolArgumentParser.ParseOptional<Arguments>(arguments, Definition.Name) ?? new Arguments(null, null);
        var options = assetApiOptions.Value;

        var paged = await assetQueryService.GetPagedAsync(
            new GetAssetsPagedQuery(
                Page: input.Page ?? 1,
                PageSize: input.PageSize ?? options.DefaultPageSize,
                MaxPageSize: options.MaxPageSize),
            cancellationToken);

        return new McpToolInvocationResult(McpAssetToolModelMapper.ToView(paged));
    }

    private sealed record Arguments(int? Page, int? PageSize);
}

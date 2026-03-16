using System.Text.Json;
using Microsoft.Extensions.Options;
using NekoHub.Api.Configuration;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Queries;
using NekoHub.Application.Assets.Services;
using NekoHub.Domain.Assets;

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
                },
                ["query"] = McpAssetToolSchemas.NullableString,
                ["contentType"] = McpAssetToolSchemas.NullableString,
                ["status"] = McpAssetToolSchemas.NullableString,
                ["orderBy"] = McpAssetToolSchemas.NullableString,
                ["orderDirection"] = McpAssetToolSchemas.NullableString
            },
            additionalProperties = false
        })
    {
        Title = "List Assets",
        Description = "Return a paged asset list with optional filtering and ordering using the stable read model.",
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
        var input = McpToolArgumentParser.ParseOptional<Arguments>(arguments, Definition.Name) ?? new Arguments(null, null, null, null, null, null, null);
        var options = assetApiOptions.Value;

        var paged = await assetQueryService.GetPagedAsync(
            new GetAssetsPagedQuery(
                Page: input.Page ?? 1,
                PageSize: input.PageSize ?? options.DefaultPageSize,
                MaxPageSize: options.MaxPageSize,
                Query: input.Query,
                ContentType: input.ContentType,
                Status: ResolveStatus(input.Status),
                SortBy: ResolveSortBy(input.OrderBy),
                SortDirection: ResolveSortDirection(input.OrderDirection)),
            cancellationToken);

        return new McpToolInvocationResult(McpAssetToolModelMapper.ToView(paged));
    }

    private static AssetStatus? ResolveStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return Enum.TryParse<AssetStatus>(status.Trim(), ignoreCase: true, out var parsedStatus)
            ? parsedStatus
            : null;
    }

    private static AssetListSortBy ResolveSortBy(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return AssetListSortBy.CreatedAtUtc;
        }

        if (orderBy.Equals("size", StringComparison.OrdinalIgnoreCase))
        {
            return AssetListSortBy.Size;
        }

        return AssetListSortBy.CreatedAtUtc;
    }

    private static AssetListSortDirection ResolveSortDirection(string? orderDirection)
    {
        if (string.Equals(orderDirection, "asc", StringComparison.OrdinalIgnoreCase))
        {
            return AssetListSortDirection.Asc;
        }

        return AssetListSortDirection.Desc;
    }

    private sealed record Arguments(
        int? Page,
        int? PageSize,
        string? Query,
        string? ContentType,
        string? Status,
        string? OrderBy,
        string? OrderDirection);
}

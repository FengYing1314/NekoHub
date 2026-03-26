using NekoHub.Domain.Assets;

namespace NekoHub.Application.Assets.Queries;

public enum AssetListSortBy
{
    CreatedAtUtc = 1,
    Size = 2
}

public enum AssetListSortDirection
{
    Desc = 1,
    Asc = 2
}

public sealed record GetAssetsPagedQuery(
    int Page,
    int PageSize,
    int MaxPageSize,
    string? Query,
    string? ContentType,
    AssetStatus? Status,
    AssetListSortBy SortBy,
    AssetListSortDirection SortDirection);

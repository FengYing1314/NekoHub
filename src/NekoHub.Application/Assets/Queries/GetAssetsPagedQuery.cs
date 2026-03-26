namespace NekoHub.Application.Assets.Queries;

public enum AssetListSortBy
{
    CreatedAt = 1,
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
    string? Keyword,
    string? ContentType,
    AssetListSortBy SortBy,
    AssetListSortDirection SortDirection);

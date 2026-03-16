namespace NekoHub.Application.Assets.Queries.Dtos;

public sealed record AssetPagedQueryDto(
    IReadOnlyList<AssetListItemQueryDto> Items,
    int Page,
    int PageSize,
    long Total);

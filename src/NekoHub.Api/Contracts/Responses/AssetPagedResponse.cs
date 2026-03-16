namespace NekoHub.Api.Contracts.Responses;

public sealed record AssetPagedResponse(
    IReadOnlyList<AssetListItemResponse> Items,
    int Page,
    int PageSize,
    long Total);

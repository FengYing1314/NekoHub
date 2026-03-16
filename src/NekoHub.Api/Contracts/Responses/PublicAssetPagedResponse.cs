namespace NekoHub.Api.Contracts.Responses;

public sealed record PublicAssetPagedResponse(
    IReadOnlyList<PublicAssetListItemResponse> Items,
    int Page,
    int PageSize,
    long Total);

namespace NekoHub.Api.Contracts.Responses;

public sealed record BatchDeleteAssetsResponse(
    int RequestedCount,
    int DeletedCount,
    IReadOnlyList<Guid> NotFoundIds);

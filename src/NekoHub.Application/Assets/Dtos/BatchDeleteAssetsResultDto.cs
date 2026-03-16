namespace NekoHub.Application.Assets.Dtos;

public sealed record BatchDeleteAssetsResultDto(
    int RequestedCount,
    int DeletedCount,
    IReadOnlyList<Guid> NotFoundIds);

using NekoHub.Domain.Assets;

namespace NekoHub.Application.Assets.Queries.Dtos;

public sealed record AssetListItemQueryDto(
    Guid Id,
    AssetType Type,
    AssetStatus Status,
    string? OriginalFileName,
    string ContentType,
    long Size,
    int? Width,
    int? Height,
    string StorageProvider,
    string? PublicUrl,
    bool IsPublic,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

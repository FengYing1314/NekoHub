using NekoHub.Domain.Assets;

namespace NekoHub.Api.Contracts.Responses;

public sealed record AssetListItemResponse(
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

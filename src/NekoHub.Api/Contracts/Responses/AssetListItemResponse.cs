using NekoHub.Domain.Assets;

namespace NekoHub.Api.Contracts.Responses;

public sealed record AssetListItemResponse(
    Guid Id,
    AssetType Type,
    AssetStatus Status,
    string OriginalFileName,
    string ContentType,
    long Size,
    int? Width,
    int? Height,
    string StorageProvider,
    string? PublicUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

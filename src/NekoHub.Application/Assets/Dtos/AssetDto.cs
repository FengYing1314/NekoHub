using NekoHub.Domain.Assets;

namespace NekoHub.Application.Assets.Dtos;

public sealed record AssetDto(
    Guid Id,
    AssetType Type,
    AssetStatus Status,
    string OriginalFileName,
    string? StoredFileName,
    string ContentType,
    string Extension,
    long Size,
    int? Width,
    int? Height,
    string? ChecksumSha256,
    string StorageProvider,
    string StorageKey,
    string? PublicUrl,
    string? Description,
    string? AltText,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

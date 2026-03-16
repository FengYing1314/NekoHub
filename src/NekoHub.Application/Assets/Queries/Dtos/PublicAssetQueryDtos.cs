using NekoHub.Domain.Assets;

namespace NekoHub.Application.Assets.Queries.Dtos;

public sealed record PublicAssetListItemQueryDto(
    Guid Id,
    AssetType Type,
    string? OriginalFileName,
    string ContentType,
    long Size,
    int? Width,
    int? Height,
    string? PublicUrl,
    string? Description,
    string? AltText,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PublicAssetDerivativeSummaryQueryDto(
    string Kind,
    string ContentType,
    string Extension,
    long Size,
    int? Width,
    int? Height,
    string? PublicUrl,
    DateTimeOffset CreatedAtUtc);

public sealed record PublicAssetQueryDto(
    Guid Id,
    AssetType Type,
    string? OriginalFileName,
    string ContentType,
    string Extension,
    long Size,
    int? Width,
    int? Height,
    string? PublicUrl,
    string? Description,
    string? AltText,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<PublicAssetDerivativeSummaryQueryDto> Derivatives);

public sealed record PublicAssetPagedQueryDto(
    IReadOnlyList<PublicAssetListItemQueryDto> Items,
    int Page,
    int PageSize,
    long Total);

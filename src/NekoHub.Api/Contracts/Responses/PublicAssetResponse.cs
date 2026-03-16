using NekoHub.Domain.Assets;

namespace NekoHub.Api.Contracts.Responses;

public sealed record PublicAssetResponse(
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
    IReadOnlyList<PublicAssetDerivativeSummaryResponse> Derivatives);

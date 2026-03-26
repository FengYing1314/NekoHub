using NekoHub.Domain.Assets;

namespace NekoHub.Api.Contracts.Responses;

public sealed record AssetResponse(
    Guid Id,
    AssetType Type,
    AssetStatus Status,
    string? OriginalFileName,
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
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AssetDerivativeSummaryResponse> Derivatives,
    IReadOnlyList<AssetStructuredResultSummaryResponse> StructuredResults,
    AssetLatestExecutionSummaryResponse? LatestExecutionSummary);

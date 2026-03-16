namespace NekoHub.Api.Contracts.Responses;

public sealed record PublicAssetDerivativeSummaryResponse(
    string Kind,
    string ContentType,
    string Extension,
    long Size,
    int? Width,
    int? Height,
    string? PublicUrl,
    DateTimeOffset CreatedAtUtc);

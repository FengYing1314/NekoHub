namespace NekoHub.Api.Contracts.Responses;

public sealed record AssetDerivativeSummaryResponse(
    string Kind,
    string ContentType,
    string Extension,
    long Size,
    int? Width,
    int? Height,
    string? PublicUrl,
    DateTimeOffset CreatedAtUtc);

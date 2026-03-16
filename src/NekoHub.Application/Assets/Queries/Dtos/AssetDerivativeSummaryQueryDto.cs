namespace NekoHub.Application.Assets.Queries.Dtos;

public sealed record AssetDerivativeSummaryQueryDto(
    string Kind,
    string ContentType,
    string Extension,
    long Size,
    int? Width,
    int? Height,
    string? PublicUrl,
    DateTimeOffset CreatedAtUtc);

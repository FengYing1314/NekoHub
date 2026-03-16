namespace NekoHub.Application.Assets.Queries.Dtos;

public sealed record AssetStructuredResultSummaryQueryDto(
    string Kind,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc);

namespace NekoHub.Api.Contracts.Responses;

public sealed record AssetStructuredResultSummaryResponse(
    string Kind,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc);

namespace NekoHub.Api.Contracts.Responses;

public sealed record DeleteAssetResponse(
    Guid Id,
    string Status,
    DateTimeOffset DeletedAtUtc);

namespace NekoHub.Api.Contracts.Responses;

public sealed record DeleteStorageProviderProfileResponse(
    Guid Id,
    bool WasDefault,
    string Status,
    DateTimeOffset DeletedAtUtc);

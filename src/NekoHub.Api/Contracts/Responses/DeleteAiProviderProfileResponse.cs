namespace NekoHub.Api.Contracts.Responses;

public sealed record DeleteAiProviderProfileResponse(
    Guid Id,
    bool WasActive,
    string Status,
    DateTimeOffset DeletedAtUtc);

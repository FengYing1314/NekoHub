namespace NekoHub.Application.Ai.Dtos;

public sealed record DeleteAiProviderProfileResultDto(
    Guid Id,
    bool WasActive,
    string Status,
    DateTimeOffset DeletedAtUtc);

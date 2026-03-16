namespace NekoHub.Api.Contracts.Responses;

public sealed record AiProviderProfileResponse(
    Guid Id,
    string Name,
    string ApiBaseUrl,
    string ApiKey,
    string ModelName,
    string DefaultSystemPrompt,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

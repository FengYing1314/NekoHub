namespace NekoHub.Application.Ai.Queries.Dtos;

public sealed record AiProviderProfileQueryDto(
    Guid Id,
    string Name,
    string ApiBaseUrl,
    string ApiKey,
    string ModelName,
    string DefaultSystemPrompt,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

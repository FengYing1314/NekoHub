namespace NekoHub.Application.Ai.Queries.Dtos;

public sealed record AiProviderRuntimeProfileDto(
    Guid Id,
    string Name,
    string ApiBaseUrl,
    string ApiKey,
    string ModelName,
    string DefaultSystemPrompt);

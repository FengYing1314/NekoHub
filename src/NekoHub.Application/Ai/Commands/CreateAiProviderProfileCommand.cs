namespace NekoHub.Application.Ai.Commands;

public sealed record CreateAiProviderProfileCommand(
    string? Name,
    string? ApiBaseUrl,
    string? ApiKey,
    string? ModelName,
    string? DefaultSystemPrompt,
    bool? IsActive);

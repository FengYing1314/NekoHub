namespace NekoHub.Application.Ai.Commands;

public sealed record TestAiProviderProfileCommand(
    Guid? ProfileId,
    string? ApiBaseUrl,
    string? ApiKey,
    string? ModelName,
    string? DefaultSystemPrompt);

using NekoHub.Application.Common.Models;

namespace NekoHub.Application.Ai.Commands;

public sealed record UpdateAiProviderProfileCommand(
    Guid ProfileId,
    OptionalValue<string?> Name,
    OptionalValue<string?> ApiBaseUrl,
    OptionalValue<string?> ApiKey,
    OptionalValue<string?> ModelName,
    OptionalValue<string?> DefaultSystemPrompt,
    OptionalValue<bool> IsActive);

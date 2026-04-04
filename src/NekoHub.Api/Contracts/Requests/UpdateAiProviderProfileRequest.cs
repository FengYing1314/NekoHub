using NekoHub.Application.Common.Models;

namespace NekoHub.Api.Contracts.Requests;

public sealed class UpdateAiProviderProfileRequest
{
    public OptionalValue<string?> Name { get; init; }

    public OptionalValue<string?> ApiBaseUrl { get; init; }

    public OptionalValue<string?> ApiKey { get; init; }

    public OptionalValue<string?> ModelName { get; init; }

    public OptionalValue<string?> DefaultSystemPrompt { get; init; }

    public OptionalValue<bool> IsActive { get; init; }
}

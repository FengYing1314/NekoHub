namespace NekoHub.Api.Contracts.Requests;

public sealed class CreateAiProviderProfileRequest
{
    public string? Name { get; init; }

    public string? ApiBaseUrl { get; init; }

    public string? ApiKey { get; init; }

    public string? ModelName { get; init; }

    public string? DefaultSystemPrompt { get; init; }

    public bool? IsActive { get; init; }
}

namespace NekoHub.Api.Contracts.Requests;

public sealed class TestAiProviderProfileRequest
{
    public Guid? ProfileId { get; init; }

    public string? ApiBaseUrl { get; init; }

    public string? ApiKey { get; init; }

    public string? ModelName { get; init; }

    public string? DefaultSystemPrompt { get; init; }
}

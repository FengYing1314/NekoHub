namespace NekoHub.Domain.Ai;

public sealed class AiProviderProfile
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string ApiBaseUrl { get; private set; } = string.Empty;

    public string ApiKey { get; private set; } = string.Empty;

    public string ApiKeyMasked { get; private set; } = string.Empty;

    public string ModelName { get; private set; } = string.Empty;

    public string DefaultSystemPrompt { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private AiProviderProfile()
    {
    }

    public AiProviderProfile(
        Guid id,
        string name,
        string apiBaseUrl,
        string apiKey,
        string apiKeyMasked,
        string modelName,
        string defaultSystemPrompt,
        bool isActive)
    {
        Id = id;
        Name = NormalizeRequired(name, nameof(name));
        ApiBaseUrl = NormalizeUrl(apiBaseUrl);
        ApiKey = NormalizeRequired(apiKey, nameof(apiKey));
        ApiKeyMasked = NormalizeRequired(apiKeyMasked, nameof(apiKeyMasked));
        ModelName = NormalizeRequired(modelName, nameof(modelName));
        DefaultSystemPrompt = NormalizeRequired(defaultSystemPrompt, nameof(defaultSystemPrompt));
        IsActive = isActive;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Rename(string name)
    {
        Name = NormalizeRequired(name, nameof(name));
        Touch();
    }

    public void UpdateConnection(string apiBaseUrl, string? apiKey, string? apiKeyMasked, string modelName)
    {
        ApiBaseUrl = NormalizeUrl(apiBaseUrl);
        ModelName = NormalizeRequired(modelName, nameof(modelName));

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            ApiKey = NormalizeRequired(apiKey, nameof(apiKey));
        }

        if (!string.IsNullOrWhiteSpace(apiKeyMasked))
        {
            ApiKeyMasked = NormalizeRequired(apiKeyMasked, nameof(apiKeyMasked));
        }

        Touch();
    }

    public void UpdatePrompt(string defaultSystemPrompt)
    {
        DefaultSystemPrompt = NormalizeRequired(defaultSystemPrompt, nameof(defaultSystemPrompt));
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeUrl(string value)
    {
        var normalized = NormalizeRequired(value, nameof(ApiBaseUrl));
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            throw new ArgumentException("ApiBaseUrl must be an absolute URL.", nameof(ApiBaseUrl));
        }

        return normalized.TrimEnd('/');
    }
}

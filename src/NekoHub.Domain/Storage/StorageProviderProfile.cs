namespace NekoHub.Domain.Storage;

public sealed class StorageProviderProfile
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? DisplayName { get; private set; }

    public string ProviderType { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public bool IsDefault { get; private set; }

    public bool SupportsPublicRead { get; private set; }

    public bool SupportsPrivateRead { get; private set; }

    public bool SupportsVisibilityToggle { get; private set; }

    public bool SupportsDelete { get; private set; }

    public bool SupportsDirectPublicUrl { get; private set; }

    public bool RequiresAccessProxy { get; private set; }

    public bool RecommendedForPrimaryStorage { get; private set; }

    public bool IsPlatformBacked { get; private set; }

    public bool IsExperimental { get; private set; }

    public bool RequiresTokenForPrivateRead { get; private set; }

    public string ConfigurationJson { get; private set; } = "{}";

    public string? SecretConfigurationJson { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private StorageProviderProfile()
    {
    }

    public StorageProviderProfile(
        Guid id,
        string name,
        string providerType,
        string configurationJson,
        StorageProviderCapabilities capabilities,
        string? displayName = null,
        bool isEnabled = true,
        bool isDefault = false,
        string? secretConfigurationJson = null)
    {
        Id = id;
        Name = NormalizeRequired(name, nameof(name));
        DisplayName = NormalizeOptional(displayName);
        ProviderType = NormalizeRequired(providerType, nameof(providerType));
        IsEnabled = isEnabled;
        IsDefault = isDefault;
        ConfigurationJson = NormalizeConfigurationJson(configurationJson);
        SecretConfigurationJson = NormalizeOptional(secretConfigurationJson);
        SetCapabilities(capabilities);
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public StorageProviderCapabilities GetCapabilities()
    {
        return new StorageProviderCapabilities(
            SupportsPublicRead,
            SupportsPrivateRead,
            SupportsVisibilityToggle,
            SupportsDelete,
            SupportsDirectPublicUrl,
            RequiresAccessProxy,
            RecommendedForPrimaryStorage,
            IsPlatformBacked,
            IsExperimental,
            RequiresTokenForPrivateRead);
    }

    public void Rename(string name, string? displayName)
    {
        Name = NormalizeRequired(name, nameof(name));
        DisplayName = NormalizeOptional(displayName);
        Touch();
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        Touch();
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        Touch();
    }

    public void UpdateConfiguration(string configurationJson, string? secretConfigurationJson = null)
    {
        ConfigurationJson = NormalizeConfigurationJson(configurationJson);
        SecretConfigurationJson = NormalizeOptional(secretConfigurationJson);
        Touch();
    }

    public void UpdateProviderType(string providerType, StorageProviderCapabilities capabilities)
    {
        ProviderType = NormalizeRequired(providerType, nameof(providerType));
        SetCapabilities(capabilities);
        Touch();
    }

    public void ApplyCapabilitySnapshot(StorageProviderCapabilities capabilities)
    {
        SetCapabilities(capabilities);
        Touch();
    }

    private void SetCapabilities(StorageProviderCapabilities capabilities)
    {
        SupportsPublicRead = capabilities.SupportsPublicRead;
        SupportsPrivateRead = capabilities.SupportsPrivateRead;
        SupportsVisibilityToggle = capabilities.SupportsVisibilityToggle;
        SupportsDelete = capabilities.SupportsDelete;
        SupportsDirectPublicUrl = capabilities.SupportsDirectPublicUrl;
        RequiresAccessProxy = capabilities.RequiresAccessProxy;
        RecommendedForPrimaryStorage = capabilities.RecommendedForPrimaryStorage;
        IsPlatformBacked = capabilities.IsPlatformBacked;
        IsExperimental = capabilities.IsExperimental;
        RequiresTokenForPrivateRead = capabilities.RequiresTokenForPrivateRead;
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

    private static string NormalizeConfigurationJson(string configurationJson)
    {
        return string.IsNullOrWhiteSpace(configurationJson)
            ? "{}"
            : configurationJson.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}

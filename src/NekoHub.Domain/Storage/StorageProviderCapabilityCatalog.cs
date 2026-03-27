namespace NekoHub.Domain.Storage;

public static class StorageProviderCapabilityCatalog
{
    private static readonly IReadOnlyDictionary<string, StorageProviderCapabilities> CapabilitiesByProviderType =
        new Dictionary<string, StorageProviderCapabilities>(StringComparer.OrdinalIgnoreCase)
        {
            [StorageProviderTypes.Local] = new(
                SupportsPublicRead: true,
                SupportsPrivateRead: true,
                SupportsVisibilityToggle: true,
                SupportsDelete: true,
                SupportsDirectPublicUrl: false,
                RequiresAccessProxy: true,
                RecommendedForPrimaryStorage: true,
                IsPlatformBacked: false,
                IsExperimental: false,
                RequiresTokenForPrivateRead: false),
            [StorageProviderTypes.S3Compatible] = new(
                SupportsPublicRead: true,
                SupportsPrivateRead: true,
                SupportsVisibilityToggle: true,
                SupportsDelete: true,
                SupportsDirectPublicUrl: true,
                RequiresAccessProxy: false,
                RecommendedForPrimaryStorage: true,
                IsPlatformBacked: false,
                IsExperimental: false,
                RequiresTokenForPrivateRead: false),
            [StorageProviderTypes.GitHubReleases] = new(
                SupportsPublicRead: true,
                SupportsPrivateRead: false,
                SupportsVisibilityToggle: false,
                SupportsDelete: false,
                SupportsDirectPublicUrl: true,
                RequiresAccessProxy: false,
                RecommendedForPrimaryStorage: false,
                IsPlatformBacked: true,
                IsExperimental: true,
                RequiresTokenForPrivateRead: false),
            [StorageProviderTypes.GitHubRepo] = new(
                SupportsPublicRead: true,
                SupportsPrivateRead: true,
                SupportsVisibilityToggle: false,
                SupportsDelete: false,
                SupportsDirectPublicUrl: true,
                RequiresAccessProxy: true,
                RecommendedForPrimaryStorage: false,
                IsPlatformBacked: true,
                IsExperimental: true,
                RequiresTokenForPrivateRead: true)
        };

    public static bool TryGet(string providerType, out StorageProviderCapabilities capabilities)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            capabilities = default!;
            return false;
        }

        return CapabilitiesByProviderType.TryGetValue(providerType.Trim(), out capabilities!);
    }

    public static StorageProviderCapabilities GetRequired(string providerType)
    {
        if (TryGet(providerType, out var capabilities))
        {
            return capabilities;
        }

        throw new InvalidOperationException($"Unsupported storage provider type '{providerType}'.");
    }

    public static IReadOnlyDictionary<string, StorageProviderCapabilities> GetAll()
    {
        return CapabilitiesByProviderType;
    }
}

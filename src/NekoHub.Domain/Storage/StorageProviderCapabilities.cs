namespace NekoHub.Domain.Storage;

public sealed record StorageProviderCapabilities(
    bool SupportsPublicRead,
    bool SupportsPrivateRead,
    bool SupportsVisibilityToggle,
    bool SupportsDelete,
    bool SupportsDirectPublicUrl,
    bool RequiresAccessProxy,
    bool RecommendedForPrimaryStorage,
    bool IsPlatformBacked = false,
    bool IsExperimental = false,
    bool RequiresTokenForPrivateRead = false);

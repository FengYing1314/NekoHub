namespace NekoHub.Api.Contracts.Responses;

public sealed record StorageProviderCapabilitiesResponse(
    bool SupportsPublicRead,
    bool SupportsPrivateRead,
    bool SupportsVisibilityToggle,
    bool SupportsDelete,
    bool SupportsDirectPublicUrl,
    bool RequiresAccessProxy,
    bool RecommendedForPrimaryStorage,
    bool IsPlatformBacked,
    bool IsExperimental,
    bool RequiresTokenForPrivateRead);

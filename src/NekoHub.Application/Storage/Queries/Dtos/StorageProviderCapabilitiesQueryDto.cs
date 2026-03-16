namespace NekoHub.Application.Storage.Queries.Dtos;

public sealed record StorageProviderCapabilitiesQueryDto(
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

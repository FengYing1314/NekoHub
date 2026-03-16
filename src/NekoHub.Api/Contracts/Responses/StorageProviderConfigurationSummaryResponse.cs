namespace NekoHub.Api.Contracts.Responses;

public sealed record StorageProviderConfigurationSummaryResponse(
    string? ProviderName,
    string? RootPath,
    string? EndpointHost,
    string? BucketOrContainer,
    string? Region,
    string? PublicBaseUrl,
    bool? ForcePathStyle,
    string? Owner,
    string? Repository,
    string? Reference,
    string? ReleaseTagMode,
    string? FixedTag,
    string? PathPrefix,
    string? VisibilityPolicy,
    string? BasePath,
    string? AssetPathPrefix,
    string? ApiBaseUrl,
    string? RawBaseUrl);

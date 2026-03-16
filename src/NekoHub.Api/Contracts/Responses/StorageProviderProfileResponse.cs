namespace NekoHub.Api.Contracts.Responses;

public sealed record StorageProviderProfileResponse(
    Guid Id,
    string Name,
    string? DisplayName,
    string ProviderType,
    bool IsEnabled,
    bool IsDefault,
    StorageProviderCapabilitiesResponse Capabilities,
    StorageProviderConfigurationSummaryResponse ConfigurationSummary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

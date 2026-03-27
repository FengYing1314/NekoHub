namespace NekoHub.Api.Contracts.Responses;

public sealed record StorageRuntimeSummaryResponse(
    string ProviderType,
    string ProviderName,
    StorageProviderCapabilitiesResponse Capabilities,
    bool IsConfigurationDriven,
    bool? MatchesDefaultProfileType);

namespace NekoHub.Application.Storage.Queries.Dtos;

public sealed record StorageRuntimeSummaryQueryDto(
    string ProviderType,
    string ProviderName,
    StorageProviderCapabilitiesQueryDto Capabilities,
    bool IsConfigurationDriven,
    bool? MatchesDefaultProfileType);

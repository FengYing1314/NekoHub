namespace NekoHub.Application.Storage.Queries.Dtos;

public sealed record StorageProviderProfileQueryDto(
    Guid Id,
    string Name,
    string? DisplayName,
    string ProviderType,
    bool IsEnabled,
    bool IsDefault,
    StorageProviderCapabilitiesQueryDto Capabilities,
    StorageProviderConfigurationSummaryQueryDto ConfigurationSummary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Storage.Queries;
using NekoHub.Application.Storage.Queries.Dtos;

namespace NekoHub.Application.Storage.Services;

public sealed class StorageProviderQueryService(
    IStorageProviderProfileRepository storageProviderProfileRepository,
    IAssetStorageResolver assetStorageResolver,
    IStorageProviderProfileRuntimeFactory storageProviderProfileRuntimeFactory) : IStorageProviderQueryService
{
    public async Task<StorageProviderOverviewQueryDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await storageProviderProfileRepository.ListAsync(cancellationToken);
        var defaultWriteProfile = await storageProviderProfileRepository.GetDefaultAsync(cancellationToken);

        var profileDtos = profiles
            .Select(StorageProviderQueryMapper.ToProfileDto)
            .ToList();

        var defaultWriteProfileDto = defaultWriteProfile is null
            ? null
            : StorageProviderQueryMapper.ToProfileDto(defaultWriteProfile);

        var runtimeDto = BuildRuntimeSummary(defaultWriteProfile);
        var alignmentDto = BuildAlignmentStatus(defaultWriteProfileDto);
        return new StorageProviderOverviewQueryDto(
            Profiles: profileDtos,
            DefaultProfile: defaultWriteProfileDto,
            DefaultWriteProfile: defaultWriteProfileDto,
            Runtime: runtimeDto,
            Alignment: alignmentDto);
    }

    private StorageRuntimeSummaryQueryDto BuildRuntimeSummary(
        Domain.Storage.StorageProviderProfile? defaultWriteProfile)
    {
        if (defaultWriteProfile is not null)
        {
            var runtimeDescriptor = storageProviderProfileRuntimeFactory.Describe(defaultWriteProfile);

            return new StorageRuntimeSummaryQueryDto(
                ProviderType: runtimeDescriptor.ProviderType,
                ProviderName: runtimeDescriptor.ProviderName,
                Capabilities: StorageProviderQueryMapper.ToCapabilitiesDto(runtimeDescriptor.Capabilities),
                IsConfigurationDriven: false,
                MatchesDefaultProfileType: true);
        }

        var runtimeStorage = assetStorageResolver.ResolveDefault();
        return new StorageRuntimeSummaryQueryDto(
            ProviderType: runtimeStorage.ProviderType,
            ProviderName: runtimeStorage.ProviderName,
            Capabilities: StorageProviderQueryMapper.ToCapabilitiesDto(runtimeStorage.Capabilities),
            IsConfigurationDriven: true,
            MatchesDefaultProfileType: null);
    }

    private static StorageRuntimeAlignmentStatusQueryDto BuildAlignmentStatus(
        StorageProviderProfileQueryDto? defaultProfile)
    {
        const string configurationSelectionSource = "configuration";
        const string databaseSelectionSource = "database-default-profile";

        if (defaultProfile is null)
        {
            return new StorageRuntimeAlignmentStatusQueryDto(
                RuntimeSelectionSource: configurationSelectionSource,
                HasDefaultProfile: false,
                IsDefaultProfileEnabled: null,
                ProviderTypeMatchesDefaultProfile: null,
                Code: "db_default_profile_missing",
                Message: "No default write profile is set in database. Runtime provider is selected from configuration.");
        }

        if (!defaultProfile.IsEnabled)
        {
            return new StorageRuntimeAlignmentStatusQueryDto(
                RuntimeSelectionSource: databaseSelectionSource,
                HasDefaultProfile: true,
                IsDefaultProfileEnabled: false,
                ProviderTypeMatchesDefaultProfile: true,
                Code: "db_default_profile_disabled",
                Message: "Database default write profile is disabled. Uploads without an explicit storage profile will be rejected until the default profile is enabled or replaced.");
        }

        return new StorageRuntimeAlignmentStatusQueryDto(
            RuntimeSelectionSource: databaseSelectionSource,
            HasDefaultProfile: true,
            IsDefaultProfileEnabled: true,
            ProviderTypeMatchesDefaultProfile: true,
            Code: "runtime_matches_db_default_provider_type",
            Message: "Runtime write target is resolved from the database default write profile.");
    }
}

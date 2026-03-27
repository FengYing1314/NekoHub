using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Storage.Queries;
using NekoHub.Application.Storage.Queries.Dtos;

namespace NekoHub.Application.Storage.Services;

public sealed class StorageProviderQueryService(
    IStorageProviderProfileRepository storageProviderProfileRepository,
    IAssetStorageResolver assetStorageResolver) : IStorageProviderQueryService
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

        var runtimeStorage = assetStorageResolver.ResolveDefault();
        var runtimeDto = new StorageRuntimeSummaryQueryDto(
            ProviderType: runtimeStorage.ProviderType,
            ProviderName: runtimeStorage.ProviderName,
            Capabilities: StorageProviderQueryMapper.ToCapabilitiesDto(runtimeStorage.Capabilities),
            IsConfigurationDriven: true,
            MatchesDefaultProfileType: defaultWriteProfileDto is null
                ? null
                : string.Equals(
                    runtimeStorage.ProviderType,
                    defaultWriteProfileDto.ProviderType,
                    StringComparison.OrdinalIgnoreCase));

        var alignmentDto = BuildAlignmentStatus(runtimeDto, defaultWriteProfileDto);
        return new StorageProviderOverviewQueryDto(
            Profiles: profileDtos,
            DefaultProfile: defaultWriteProfileDto,
            DefaultWriteProfile: defaultWriteProfileDto,
            Runtime: runtimeDto,
            Alignment: alignmentDto);
    }

    private static StorageRuntimeAlignmentStatusQueryDto BuildAlignmentStatus(
        StorageRuntimeSummaryQueryDto runtime,
        StorageProviderProfileQueryDto? defaultProfile)
    {
        const string runtimeSelectionSource = "configuration";

        if (defaultProfile is null)
        {
            return new StorageRuntimeAlignmentStatusQueryDto(
                RuntimeSelectionSource: runtimeSelectionSource,
                HasDefaultProfile: false,
                IsDefaultProfileEnabled: null,
                ProviderTypeMatchesDefaultProfile: null,
                Code: "db_default_profile_missing",
                Message: "No default write profile is set in database. Runtime provider is selected from configuration.");
        }

        var providerTypeMatchesDefaultProfile = string.Equals(
            runtime.ProviderType,
            defaultProfile.ProviderType,
            StringComparison.OrdinalIgnoreCase);

        if (!defaultProfile.IsEnabled)
        {
            return new StorageRuntimeAlignmentStatusQueryDto(
                RuntimeSelectionSource: runtimeSelectionSource,
                HasDefaultProfile: true,
                IsDefaultProfileEnabled: false,
                ProviderTypeMatchesDefaultProfile: providerTypeMatchesDefaultProfile,
                Code: "db_default_profile_disabled",
                Message: "Database default write profile is disabled. Runtime background provider is still selected from configuration.");
        }

        if (providerTypeMatchesDefaultProfile)
        {
            return new StorageRuntimeAlignmentStatusQueryDto(
                RuntimeSelectionSource: runtimeSelectionSource,
                HasDefaultProfile: true,
                IsDefaultProfileEnabled: true,
                ProviderTypeMatchesDefaultProfile: true,
                Code: "runtime_matches_db_default_provider_type",
                Message: "Runtime provider type matches the database default write profile type, but runtime is still configuration-driven (legacy background selection).");
        }

        return new StorageRuntimeAlignmentStatusQueryDto(
            RuntimeSelectionSource: runtimeSelectionSource,
            HasDefaultProfile: true,
            IsDefaultProfileEnabled: true,
            ProviderTypeMatchesDefaultProfile: false,
            Code: "runtime_mismatches_db_default_provider_type",
            Message: "Runtime provider type differs from the database default write profile type. Runtime is still configuration-driven (legacy background selection).");
    }
}

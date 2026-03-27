using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Application.Assets.Services;

public sealed class AssetStorageTargetSelector(
    IStorageProviderProfileRepository storageProviderProfileRepository,
    IAssetStorageResolver assetStorageResolver) : IAssetStorageTargetSelector
{
    public async Task<AssetStorageTargetSelectionResult> ResolveWriteTargetAsync(
        Guid? requestedProfileId,
        CancellationToken cancellationToken = default)
    {
        if (requestedProfileId.HasValue)
        {
            var explicitProfile = await ResolveEnabledProfileForWriteOrThrowAsync(
                requestedProfileId.Value,
                cancellationToken);

            var explicitStorage = ResolveStorageByProfileTypeOrThrow(explicitProfile.ProviderType, explicitProfile.Id);
            EnsureSupportsWriteOrThrow(explicitStorage, explicitProfile.Id, explicitProfile.ProviderType);

            return new AssetStorageTargetSelectionResult(
                Storage: explicitStorage,
                StorageProviderProfileId: explicitProfile.Id,
                SelectionSource: "profile-explicit");
        }

        var defaultWriteProfile = await storageProviderProfileRepository.GetDefaultAsync(cancellationToken);
        if (defaultWriteProfile is not null)
        {
            if (!defaultWriteProfile.IsEnabled)
            {
                throw new ValidationException(
                    "storage_provider_default_write_profile_disabled",
                    $"Default write profile '{defaultWriteProfile.Id}' is disabled.");
            }

            var defaultStorage = ResolveStorageByProfileTypeOrThrow(defaultWriteProfile.ProviderType, defaultWriteProfile.Id);
            EnsureSupportsWriteOrThrow(defaultStorage, defaultWriteProfile.Id, defaultWriteProfile.ProviderType);

            return new AssetStorageTargetSelectionResult(
                Storage: defaultStorage,
                StorageProviderProfileId: defaultWriteProfile.Id,
                SelectionSource: "default-write-profile");
        }

        var legacyConfiguredStorage = assetStorageResolver.ResolveDefault();
        if (!legacyConfiguredStorage.SupportsWrite)
        {
            throw new ValidationException(
                "storage_provider_runtime_default_write_not_supported",
                $"Configured runtime default provider '{legacyConfiguredStorage.ProviderName}' does not support write.");
        }

        return new AssetStorageTargetSelectionResult(
            Storage: legacyConfiguredStorage,
            StorageProviderProfileId: null,
            SelectionSource: "configuration-default");
    }

    public async Task<IAssetStorage> ResolveReadTargetAsync(
        Guid? boundProfileId,
        string legacyStorageProvider,
        CancellationToken cancellationToken = default)
    {
        if (boundProfileId.HasValue)
        {
            var boundProfile = await storageProviderProfileRepository.GetByIdAsync(boundProfileId.Value, cancellationToken);
            if (boundProfile is null)
            {
                throw new ValidationException(
                    "asset_storage_provider_profile_not_found",
                    $"Storage provider profile '{boundProfileId.Value}' bound to asset was not found.");
            }

            if (!boundProfile.IsEnabled)
            {
                throw new ValidationException(
                    "asset_storage_provider_profile_disabled",
                    $"Storage provider profile '{boundProfile.Id}' bound to asset is disabled.");
            }

            try
            {
                return assetStorageResolver.ResolveByProviderType(boundProfile.ProviderType);
            }
            catch (InvalidOperationException exception)
            {
                throw new ValidationException(
                    "asset_storage_provider_profile_unavailable",
                    $"Storage provider type '{boundProfile.ProviderType}' for profile '{boundProfile.Id}' is unavailable: {exception.Message}");
            }
        }

        try
        {
            return assetStorageResolver.Resolve(legacyStorageProvider);
        }
        catch (InvalidOperationException exception)
        {
            throw new ValidationException(
                "asset_storage_provider_legacy_unavailable",
                $"Legacy storage provider '{legacyStorageProvider}' is unavailable: {exception.Message}");
        }
    }

    private IAssetStorage ResolveStorageByProfileTypeOrThrow(string providerType, Guid profileId)
    {
        try
        {
            return assetStorageResolver.ResolveByProviderType(providerType);
        }
        catch (InvalidOperationException exception)
        {
            throw new ValidationException(
                "storage_provider_profile_write_not_supported",
                $"Storage provider profile '{profileId}' uses unsupported provider type '{providerType}' for write: {exception.Message}");
        }
    }

    private static void EnsureSupportsWriteOrThrow(IAssetStorage storage, Guid profileId, string providerType)
    {
        if (storage.SupportsWrite)
        {
            return;
        }

        throw new ValidationException(
            "storage_provider_profile_write_not_supported",
            $"Storage provider profile '{profileId}' with provider type '{providerType}' does not support write.");
    }

    private async Task<Domain.Storage.StorageProviderProfile> ResolveEnabledProfileForWriteOrThrowAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profile = await storageProviderProfileRepository.GetByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "storage_provider_profile_not_found",
                $"Storage provider profile '{profileId}' was not found.");
        }

        if (!profile.IsEnabled)
        {
            throw new ValidationException(
                "storage_provider_profile_disabled",
                $"Storage provider profile '{profileId}' is disabled.");
        }

        return profile;
    }
}

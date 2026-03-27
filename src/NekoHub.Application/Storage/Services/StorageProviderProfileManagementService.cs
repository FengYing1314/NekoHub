using System.Text.Json;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Storage.Commands;
using NekoHub.Application.Storage.Dtos;
using NekoHub.Application.Storage.Queries;
using NekoHub.Application.Storage.Queries.Dtos;
using NekoHub.Application.Storage.Validation;
using NekoHub.Domain.Storage;

namespace NekoHub.Application.Storage.Services;

public sealed class StorageProviderProfileManagementService(
    IStorageProviderProfileRepository storageProviderProfileRepository,
    IEnumerable<IStorageProviderProfileConfigurationValidator> validators) : IStorageProviderProfileManagementService
{
    private readonly IReadOnlyDictionary<string, IStorageProviderProfileConfigurationValidator> _validators =
        validators.ToDictionary(x => x.ProviderType, StringComparer.OrdinalIgnoreCase);

    public async Task<StorageProviderProfileQueryDto> CreateAsync(
        CreateStorageProviderProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var providerType = NormalizeRequired(command.ProviderType, "storage_provider_profile_provider_type_required", "ProviderType is required.");
        var name = NormalizeRequired(command.Name, "storage_provider_profile_name_required", "Name is required.");
        var displayName = NormalizeOptional(command.DisplayName);

        await EnsureUniqueNameAsync(name, null, cancellationToken);

        var validator = ResolveValidator(providerType);
        var configurationJson = StorageProviderProfileJson.NormalizeJsonElement(
            command.Configuration,
            "storage_provider_profile_configuration_required",
            "Configuration is required and must be a JSON object.");
        var secretConfigurationJson = NormalizeSecretJson(command.SecretConfiguration);
        var validated = validator.Validate(configurationJson, secretConfigurationJson);

        if (command.IsDefault && !command.IsEnabled)
        {
            throw new ValidationException(
                "storage_provider_profile_default_requires_enabled",
                "Default storage provider profiles must be enabled.");
        }

        var profile = new StorageProviderProfile(
            id: Guid.CreateVersion7(),
            name: name,
            providerType: providerType,
            configurationJson: validated.ConfigurationJson,
            capabilities: validated.Capabilities,
            displayName: displayName,
            isEnabled: command.IsEnabled,
            isDefault: false,
            secretConfigurationJson: validated.SecretConfigurationJson);

        if (command.IsDefault)
        {
            await ClearDefaultProfilesAsync(cancellationToken);
            profile.SetDefault(true);
        }

        await storageProviderProfileRepository.AddAsync(profile, cancellationToken);
        await storageProviderProfileRepository.SaveChangesAsync(cancellationToken);

        return StorageProviderQueryMapper.ToProfileDto(profile);
    }

    public async Task<StorageProviderProfileQueryDto> UpdateAsync(
        UpdateStorageProviderProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var profile = await storageProviderProfileRepository.GetByIdAsync(command.ProfileId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "storage_provider_profile_not_found",
                $"Storage provider profile '{command.ProfileId}' was not found.");
        }

        var name = command.Name.IsSet
            ? NormalizeRequired(command.Name.Value, "storage_provider_profile_name_required", "Name is required.")
            : profile.Name;
        var displayName = command.DisplayName.IsSet
            ? NormalizeOptional(command.DisplayName.Value)
            : profile.DisplayName;
        var isEnabled = command.IsEnabled.IsSet
            ? command.IsEnabled.Value
            : profile.IsEnabled;

        await EnsureUniqueNameAsync(name, profile.Id, cancellationToken);

        if (profile.IsDefault && !isEnabled)
        {
            throw new ValidationException(
                "storage_provider_profile_default_requires_enabled",
                "Default storage provider profiles must remain enabled.");
        }

        var configurationJson = command.Configuration.IsSet
            ? StorageProviderProfileJson.NormalizeJsonElement(
                command.Configuration.Value,
                "storage_provider_profile_configuration_required",
                "Configuration must be a JSON object.")
            : profile.ConfigurationJson;
        var secretConfigurationJson = command.SecretConfiguration.IsSet
            ? NormalizeSecretJson(command.SecretConfiguration.Value)
            : profile.SecretConfigurationJson;

        var validator = ResolveValidator(profile.ProviderType);
        var validated = validator.Validate(configurationJson, secretConfigurationJson);

        profile.Rename(name, displayName);
        profile.SetEnabled(isEnabled);
        profile.UpdateConfiguration(validated.ConfigurationJson, validated.SecretConfigurationJson);
        profile.ApplyCapabilitySnapshot(validated.Capabilities);

        await storageProviderProfileRepository.SaveChangesAsync(cancellationToken);
        return StorageProviderQueryMapper.ToProfileDto(profile);
    }

    public async Task<DeleteStorageProviderProfileResultDto> DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await storageProviderProfileRepository.GetByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "storage_provider_profile_not_found",
                $"Storage provider profile '{profileId}' was not found.");
        }

        var wasDefault = profile.IsDefault;
        await storageProviderProfileRepository.DeleteAsync(profile, cancellationToken);
        await storageProviderProfileRepository.SaveChangesAsync(cancellationToken);

        return new DeleteStorageProviderProfileResultDto(
            Id: profileId,
            WasDefault: wasDefault,
            Status: "deleted",
            DeletedAtUtc: DateTimeOffset.UtcNow);
    }

    public async Task<StorageProviderProfileQueryDto> SetDefaultAsync(Guid profileId, CancellationToken cancellationToken = default)
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
                "storage_provider_profile_default_requires_enabled",
                "Only enabled storage provider profiles can be set as default.");
        }

        var defaultProfiles = await storageProviderProfileRepository.ListDefaultProfilesAsync(cancellationToken);
        foreach (var defaultProfile in defaultProfiles.Where(x => x.Id != profile.Id))
        {
            defaultProfile.SetDefault(false);
        }

        profile.SetDefault(true);
        await storageProviderProfileRepository.SaveChangesAsync(cancellationToken);
        return StorageProviderQueryMapper.ToProfileDto(profile);
    }

    private async Task ClearDefaultProfilesAsync(CancellationToken cancellationToken)
    {
        var defaultProfiles = await storageProviderProfileRepository.ListDefaultProfilesAsync(cancellationToken);
        foreach (var defaultProfile in defaultProfiles)
        {
            defaultProfile.SetDefault(false);
        }
    }

    private async Task EnsureUniqueNameAsync(string name, Guid? excludeProfileId, CancellationToken cancellationToken)
    {
        if (await storageProviderProfileRepository.ExistsByNameAsync(name, excludeProfileId, cancellationToken))
        {
            throw new ValidationException(
                "storage_provider_profile_name_conflict",
                $"Storage provider profile name '{name}' already exists.");
        }
    }

    private IStorageProviderProfileConfigurationValidator ResolveValidator(string providerType)
    {
        if (_validators.TryGetValue(providerType, out var validator))
        {
            return validator;
        }

        throw new ValidationException(
            "storage_provider_profile_provider_type_unsupported",
            $"Unsupported storage provider type '{providerType}'.");
    }

    private static string NormalizeRequired(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(code, message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeSecretJson(JsonElement? secretConfiguration)
    {
        var normalized = StorageProviderProfileJson.NormalizeJsonElement(
            secretConfiguration,
            "storage_provider_profile_secret_configuration_invalid",
            "SecretConfiguration must be a JSON object.",
            allowNull: true);

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

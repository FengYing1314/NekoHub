using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Storage;

namespace NekoHub.Application.Storage.Validation;

public sealed class LocalStorageProviderProfileConfigurationValidator : IStorageProviderProfileConfigurationValidator
{
    public string ProviderType => StorageProviderTypes.Local;

    public ValidatedStorageProviderProfileConfiguration Validate(
        string configurationJson,
        string? secretConfigurationJson)
    {
        if (!StorageProviderProfileJson.IsNullOrEmptyObject(secretConfigurationJson))
        {
            throw new ValidationException(
                "storage_provider_profile_secret_not_supported",
                "Local storage profiles do not support secretConfiguration.");
        }

        var configuration = StorageProviderProfileJson.DeserializeRequiredObject<LocalStorageProfileConfiguration>(
            configurationJson,
            "storage_provider_profile_configuration_invalid",
            "Local storage configuration must be a JSON object.");

        if (string.IsNullOrWhiteSpace(configuration.RootPath))
        {
            throw new ValidationException(
                "storage_provider_profile_root_path_required",
                "Local storage configuration requires rootPath.");
        }

        if (!string.IsNullOrWhiteSpace(configuration.PublicBaseUrl)
            && !StorageProviderProfileJson.IsAbsoluteHttpUrl(configuration.PublicBaseUrl))
        {
            throw new ValidationException(
                "storage_provider_profile_public_base_url_invalid",
                "Local storage publicBaseUrl must be an absolute http/https URL.");
        }

        var normalizedConfiguration = StorageProviderProfileJson.Serialize(new LocalStorageProfileConfiguration(
            RootPath: configuration.RootPath.Trim(),
            CreateDirectoryIfMissing: configuration.CreateDirectoryIfMissing,
            PublicBaseUrl: string.IsNullOrWhiteSpace(configuration.PublicBaseUrl) ? null : configuration.PublicBaseUrl.Trim()));

        return new ValidatedStorageProviderProfileConfiguration(
            ConfigurationJson: normalizedConfiguration,
            SecretConfigurationJson: null,
            Capabilities: StorageProviderCapabilityCatalog.GetRequired(ProviderType));
    }

    private sealed record LocalStorageProfileConfiguration(
        string? RootPath,
        bool CreateDirectoryIfMissing = true,
        string? PublicBaseUrl = null);
}

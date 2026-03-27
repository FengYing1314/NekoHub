using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Storage;

namespace NekoHub.Application.Storage.Validation;

public sealed class S3CompatibleStorageProviderProfileConfigurationValidator : IStorageProviderProfileConfigurationValidator
{
    public string ProviderType => StorageProviderTypes.S3Compatible;

    public ValidatedStorageProviderProfileConfiguration Validate(
        string configurationJson,
        string? secretConfigurationJson)
    {
        var configuration = StorageProviderProfileJson.DeserializeRequiredObject<S3StorageProfileConfiguration>(
            configurationJson,
            "storage_provider_profile_configuration_invalid",
            "S3-compatible storage configuration must be a JSON object.");

        if (string.IsNullOrWhiteSpace(configuration.Endpoint)
            || !StorageProviderProfileJson.IsAbsoluteHttpUrl(configuration.Endpoint))
        {
            throw new ValidationException(
                "storage_provider_profile_endpoint_invalid",
                "S3-compatible storage configuration requires endpoint as an absolute http/https URL.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Bucket))
        {
            throw new ValidationException(
                "storage_provider_profile_bucket_required",
                "S3-compatible storage configuration requires bucket.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Region))
        {
            throw new ValidationException(
                "storage_provider_profile_region_required",
                "S3-compatible storage configuration requires region.");
        }

        if (!string.IsNullOrWhiteSpace(configuration.PublicBaseUrl)
            && !StorageProviderProfileJson.IsAbsoluteHttpUrl(configuration.PublicBaseUrl))
        {
            throw new ValidationException(
                "storage_provider_profile_public_base_url_invalid",
                "S3-compatible storage publicBaseUrl must be an absolute http/https URL.");
        }

        var secret = StorageProviderProfileJson.DeserializeRequiredObject<S3StorageSecretConfiguration>(
            string.IsNullOrWhiteSpace(secretConfigurationJson) ? "{}" : secretConfigurationJson,
            "storage_provider_profile_secret_configuration_invalid",
            "S3-compatible storage secretConfiguration must be a JSON object.");

        if (string.IsNullOrWhiteSpace(secret.AccessKey))
        {
            throw new ValidationException(
                "storage_provider_profile_access_key_required",
                "S3-compatible storage secretConfiguration requires accessKey.");
        }

        if (string.IsNullOrWhiteSpace(secret.SecretKey))
        {
            throw new ValidationException(
                "storage_provider_profile_secret_key_required",
                "S3-compatible storage secretConfiguration requires secretKey.");
        }

        var normalizedConfiguration = StorageProviderProfileJson.Serialize(new S3StorageProfileConfiguration(
            ProviderName: string.IsNullOrWhiteSpace(configuration.ProviderName) ? "s3" : configuration.ProviderName.Trim(),
            Endpoint: configuration.Endpoint.Trim(),
            Bucket: configuration.Bucket.Trim(),
            Region: configuration.Region.Trim(),
            ForcePathStyle: configuration.ForcePathStyle,
            PublicBaseUrl: string.IsNullOrWhiteSpace(configuration.PublicBaseUrl) ? null : configuration.PublicBaseUrl.Trim()));

        var normalizedSecret = StorageProviderProfileJson.Serialize(new S3StorageSecretConfiguration(
            AccessKey: secret.AccessKey.Trim(),
            SecretKey: secret.SecretKey.Trim()));

        return new ValidatedStorageProviderProfileConfiguration(
            ConfigurationJson: normalizedConfiguration,
            SecretConfigurationJson: normalizedSecret,
            Capabilities: StorageProviderCapabilityCatalog.GetRequired(ProviderType));
    }

    private sealed record S3StorageProfileConfiguration(
        string? ProviderName,
        string? Endpoint,
        string? Bucket,
        string? Region,
        bool ForcePathStyle = true,
        string? PublicBaseUrl = null);

    private sealed record S3StorageSecretConfiguration(
        string? AccessKey,
        string? SecretKey);
}

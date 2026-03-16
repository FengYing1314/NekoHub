using Microsoft.Extensions.Options;

namespace NekoHub.Infrastructure.Options;

public sealed class S3StorageOptionsValidator(IOptions<StorageOptions> storageOptions) : IValidateOptions<S3StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, S3StorageOptions options)
    {
        var configuredProvider = storageOptions.Value.Provider?.Trim();
        var providerName = options.ProviderName?.Trim();
        var shouldValidate =
            string.Equals(configuredProvider, S3StorageOptions.DefaultProviderName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuredProvider, providerName, StringComparison.OrdinalIgnoreCase);

        if (!shouldValidate)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(providerName))
        {
            errors.Add("Storage:S3:ProviderName is required.");
        }

        if (!string.Equals(configuredProvider, providerName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Storage:Provider ('{configuredProvider}') must match Storage:S3:ProviderName ('{providerName}').");
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            errors.Add("Storage:S3:Endpoint is required when S3 provider is active.");
        }
        else if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            errors.Add("Storage:S3:Endpoint must be an absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.Bucket))
        {
            errors.Add("Storage:S3:Bucket is required when S3 provider is active.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey))
        {
            errors.Add("Storage:S3:AccessKey is required when S3 provider is active.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            errors.Add("Storage:S3:SecretKey is required when S3 provider is active.");
        }

        if (!string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            && !Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("Storage:S3:PublicBaseUrl must be an absolute URI when provided.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}

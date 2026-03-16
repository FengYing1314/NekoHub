using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Storage;

namespace NekoHub.Infrastructure.Options;

public sealed class LocalStorageOptionsValidator(IOptions<StorageOptions> storageOptions) : IValidateOptions<LocalStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalStorageOptions options)
    {
        var configuredProvider = storageOptions.Value.Provider?.Trim();
        if (!string.Equals(configuredProvider, StorageProviderExtensions.LocalProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            errors.Add("Storage:Local:RootPath is required when local provider is active.");
        }

        var publicBaseUrl = storageOptions.Value.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            errors.Add("Storage:PublicBaseUrl is required when local provider is active.");
        }
        else if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("Storage:PublicBaseUrl must be an absolute URI when local provider is active.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}

using Microsoft.Extensions.Options;

namespace NekoHub.Infrastructure.Options;

public sealed class GitHubRepoStorageOptionsValidator(IOptions<StorageOptions> storageOptions)
    : IValidateOptions<GitHubRepoStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, GitHubRepoStorageOptions options)
    {
        var configuredProvider = storageOptions.Value.Provider?.Trim();
        var providerName = options.ProviderName?.Trim();
        var shouldValidate =
            string.Equals(configuredProvider, GitHubRepoStorageOptions.DefaultProviderName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuredProvider, providerName, StringComparison.OrdinalIgnoreCase);

        if (!shouldValidate)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(providerName))
        {
            errors.Add("Storage:GitHubRepo:ProviderName is required.");
        }
        else if (!string.Equals(configuredProvider, providerName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Storage:Provider ('{configuredProvider}') must match Storage:GitHubRepo:ProviderName ('{providerName}').");
        }

        if (string.IsNullOrWhiteSpace(options.Owner))
        {
            errors.Add("Storage:GitHubRepo:Owner is required when github-repo provider is active.");
        }

        if (string.IsNullOrWhiteSpace(options.Repo))
        {
            errors.Add("Storage:GitHubRepo:Repo is required when github-repo provider is active.");
        }

        if (string.IsNullOrWhiteSpace(options.Ref))
        {
            errors.Add("Storage:GitHubRepo:Ref is required when github-repo provider is active.");
        }
        else if (options.Ref.Contains(' ') || options.Ref.Contains('\\'))
        {
            errors.Add("Storage:GitHubRepo:Ref cannot contain spaces or backslashes.");
        }

        if (!IsAbsoluteHttpUrl(options.ApiBaseUrl))
        {
            errors.Add("Storage:GitHubRepo:ApiBaseUrl must be an absolute http/https URL.");
        }

        if (!IsAbsoluteHttpUrl(options.RawBaseUrl))
        {
            errors.Add("Storage:GitHubRepo:RawBaseUrl must be an absolute http/https URL.");
        }

        var visibilityPolicy = string.IsNullOrWhiteSpace(options.VisibilityPolicy)
            ? "public-only"
            : options.VisibilityPolicy.Trim().ToLowerInvariant();

        if (visibilityPolicy is not ("public-only" or "private-token"))
        {
            errors.Add("Storage:GitHubRepo:VisibilityPolicy must be 'public-only' or 'private-token'.");
        }

        if (visibilityPolicy == "private-token" && string.IsNullOrWhiteSpace(options.Token))
        {
            errors.Add("Storage:GitHubRepo:Token is required when VisibilityPolicy is 'private-token'.");
        }

        if (!string.IsNullOrWhiteSpace(options.BasePath))
        {
            var normalizedBasePath = options.BasePath.Replace('\\', '/').Trim('/');
            if (normalizedBasePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            {
                errors.Add("Storage:GitHubRepo:BasePath cannot contain relative traversal segments.");
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsAbsoluteHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

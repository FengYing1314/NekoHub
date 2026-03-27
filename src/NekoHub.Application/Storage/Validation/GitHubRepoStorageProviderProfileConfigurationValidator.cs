using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Storage;

namespace NekoHub.Application.Storage.Validation;

public sealed class GitHubRepoStorageProviderProfileConfigurationValidator : IStorageProviderProfileConfigurationValidator
{
    private const string DefaultApiBaseUrl = "https://api.github.com";
    private const string DefaultRawBaseUrl = "https://raw.githubusercontent.com";

    public string ProviderType => StorageProviderTypes.GitHubRepo;

    public ValidatedStorageProviderProfileConfiguration Validate(
        string configurationJson,
        string? secretConfigurationJson)
    {
        var configuration = StorageProviderProfileJson.DeserializeRequiredObject<GitHubRepoProfileConfiguration>(
            configurationJson,
            "storage_provider_profile_configuration_invalid",
            "GitHub repo storage configuration must be a JSON object.");

        var owner = GitHubStorageProviderProfileValidationRules.NormalizeOwner(configuration.Owner);
        var repo = GitHubStorageProviderProfileValidationRules.NormalizeRepository(configuration.Repo);
        var @ref = NormalizeRef(configuration.Ref);
        var basePath = GitHubStorageProviderProfileValidationRules.NormalizePathPrefix(configuration.BasePath);
        var visibilityPolicy = GitHubStorageProviderProfileValidationRules.NormalizeVisibilityPolicy(
            configuration.VisibilityPolicy,
            "public-only",
            "private-token");
        var apiBaseUrl = NormalizeOptionalAbsoluteHttpUrl(
            configuration.ApiBaseUrl,
            DefaultApiBaseUrl,
            "storage_provider_profile_github_repo_api_base_url_invalid",
            "github-repo apiBaseUrl must be an absolute http/https URL.");
        var rawBaseUrl = NormalizeOptionalAbsoluteHttpUrl(
            configuration.RawBaseUrl,
            DefaultRawBaseUrl,
            "storage_provider_profile_github_repo_raw_base_url_invalid",
            "github-repo rawBaseUrl must be an absolute http/https URL.");

        if (configuration.AllowDelete)
        {
            throw new ValidationException(
                "storage_provider_profile_github_repo_delete_not_supported",
                "github-repo profiles do not support delete operations.");
        }

        var secret = StorageProviderProfileJson.DeserializeRequiredObject<GitHubRepoSecretConfiguration>(
            string.IsNullOrWhiteSpace(secretConfigurationJson) ? "{}" : secretConfigurationJson,
            "storage_provider_profile_secret_configuration_invalid",
            "GitHub repo secretConfiguration must be a JSON object.");

        var token = GitHubStorageProviderProfileValidationRules.NormalizeOptionalToken(secret.Token);
        if (string.Equals(visibilityPolicy, "private-token", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(token))
        {
            throw new ValidationException(
                "storage_provider_profile_github_repo_token_required_for_private",
                "github-repo private-token visibilityPolicy requires token in secretConfiguration.");
        }

        var normalizedConfiguration = StorageProviderProfileJson.Serialize(new GitHubRepoProfileConfiguration(
            Owner: owner,
            Repo: repo,
            Ref: @ref,
            BasePath: string.IsNullOrWhiteSpace(basePath) ? null : basePath,
            VisibilityPolicy: visibilityPolicy,
            ApiBaseUrl: apiBaseUrl,
            RawBaseUrl: rawBaseUrl,
            AllowDelete: false));

        var normalizedSecret = token is null
            ? null
            : StorageProviderProfileJson.Serialize(new GitHubRepoSecretConfiguration(Token: token));

        return new ValidatedStorageProviderProfileConfiguration(
            ConfigurationJson: normalizedConfiguration,
            SecretConfigurationJson: normalizedSecret,
            Capabilities: StorageProviderCapabilityCatalog.GetRequired(ProviderType));
    }

    private static string NormalizeRef(string? @ref)
    {
        if (string.IsNullOrWhiteSpace(@ref))
        {
            throw new ValidationException(
                "storage_provider_profile_github_repo_ref_required",
                "github-repo configuration requires ref.");
        }

        var normalized = @ref.Trim();
        if (normalized.Contains(' ') || normalized.Contains('\\'))
        {
            throw new ValidationException(
                "storage_provider_profile_github_repo_ref_invalid",
                "github-repo ref cannot contain spaces or backslashes.");
        }

        return normalized;
    }

    private sealed record GitHubRepoProfileConfiguration(
        string? Owner,
        string? Repo,
        string? Ref,
        string? BasePath,
        string? VisibilityPolicy,
        string? ApiBaseUrl,
        string? RawBaseUrl,
        bool AllowDelete = false);

    private sealed record GitHubRepoSecretConfiguration(
        string? Token);

    private static string NormalizeOptionalAbsoluteHttpUrl(
        string? value,
        string fallback,
        string code,
        string message)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();

        if (!StorageProviderProfileJson.IsAbsoluteHttpUrl(normalized))
        {
            throw new ValidationException(code, message);
        }

        return normalized.TrimEnd('/');
    }
}

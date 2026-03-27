using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Storage;

namespace NekoHub.Application.Storage.Validation;

public sealed class GitHubReleasesStorageProviderProfileConfigurationValidator : IStorageProviderProfileConfigurationValidator
{
    private const string ReleaseTagModeLatest = "latest";
    private const string ReleaseTagModeFixed = "fixed";

    public string ProviderType => StorageProviderTypes.GitHubReleases;

    public ValidatedStorageProviderProfileConfiguration Validate(
        string configurationJson,
        string? secretConfigurationJson)
    {
        var configuration = StorageProviderProfileJson.DeserializeRequiredObject<GitHubReleasesProfileConfiguration>(
            configurationJson,
            "storage_provider_profile_configuration_invalid",
            "GitHub releases storage configuration must be a JSON object.");

        var owner = GitHubStorageProviderProfileValidationRules.NormalizeOwner(configuration.Owner);
        var repo = GitHubStorageProviderProfileValidationRules.NormalizeRepository(configuration.Repo);
        var releaseTagMode = NormalizeReleaseTagMode(configuration.ReleaseTagMode);
        var fixedTag = NormalizeFixedTag(releaseTagMode, configuration.FixedTag);
        var pathPrefix = GitHubStorageProviderProfileValidationRules.NormalizePathPrefix(configuration.AssetPathPrefix);
        var visibilityPolicy = GitHubStorageProviderProfileValidationRules.NormalizeVisibilityPolicy(
            configuration.VisibilityPolicy,
            "public-only",
            "public-first");

        if (configuration.AllowDelete)
        {
            throw new ValidationException(
                "storage_provider_profile_github_releases_delete_not_supported",
                "github-releases profiles do not support delete operations.");
        }

        var secret = StorageProviderProfileJson.DeserializeRequiredObject<GitHubReleasesSecretConfiguration>(
            string.IsNullOrWhiteSpace(secretConfigurationJson) ? "{}" : secretConfigurationJson,
            "storage_provider_profile_secret_configuration_invalid",
            "GitHub releases secretConfiguration must be a JSON object.");

        var token = GitHubStorageProviderProfileValidationRules.NormalizeOptionalToken(secret.Token);

        var normalizedConfiguration = StorageProviderProfileJson.Serialize(new GitHubReleasesProfileConfiguration(
            Owner: owner,
            Repo: repo,
            ReleaseTagMode: releaseTagMode,
            FixedTag: fixedTag,
            AssetPathPrefix: string.IsNullOrWhiteSpace(pathPrefix) ? null : pathPrefix,
            VisibilityPolicy: visibilityPolicy,
            AllowDelete: false));

        var normalizedSecret = token is null
            ? null
            : StorageProviderProfileJson.Serialize(new GitHubReleasesSecretConfiguration(Token: token));

        return new ValidatedStorageProviderProfileConfiguration(
            ConfigurationJson: normalizedConfiguration,
            SecretConfigurationJson: normalizedSecret,
            Capabilities: StorageProviderCapabilityCatalog.GetRequired(ProviderType));
    }

    private static string NormalizeReleaseTagMode(string? releaseTagMode)
    {
        var normalized = string.IsNullOrWhiteSpace(releaseTagMode)
            ? ReleaseTagModeLatest
            : releaseTagMode.Trim().ToLowerInvariant();

        if (normalized is ReleaseTagModeLatest or ReleaseTagModeFixed)
        {
            return normalized;
        }

        throw new ValidationException(
            "storage_provider_profile_github_releases_release_tag_mode_invalid",
            $"github-releases releaseTagMode must be '{ReleaseTagModeLatest}' or '{ReleaseTagModeFixed}'.");
    }

    private static string? NormalizeFixedTag(string releaseTagMode, string? fixedTag)
    {
        if (releaseTagMode == ReleaseTagModeFixed)
        {
            if (string.IsNullOrWhiteSpace(fixedTag))
            {
                throw new ValidationException(
                    "storage_provider_profile_github_releases_fixed_tag_required",
                    "github-releases fixed mode requires fixedTag.");
            }

            return fixedTag.Trim();
        }

        return null;
    }

    private sealed record GitHubReleasesProfileConfiguration(
        string? Owner,
        string? Repo,
        string? ReleaseTagMode,
        string? FixedTag,
        string? AssetPathPrefix,
        string? VisibilityPolicy,
        bool AllowDelete = false);

    private sealed record GitHubReleasesSecretConfiguration(
        string? Token);
}

using System.Text.RegularExpressions;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Application.Storage.Validation;

internal static partial class GitHubStorageProviderProfileValidationRules
{
    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubSegmentRegex();

    public static string NormalizeOwner(string? owner)
    {
        return NormalizeGitHubSegment(
            owner,
            "storage_provider_profile_github_owner_required",
            "GitHub configuration requires owner.",
            "storage_provider_profile_github_owner_invalid",
            "GitHub owner contains invalid characters.");
    }

    public static string NormalizeRepository(string? repository)
    {
        return NormalizeGitHubSegment(
            repository,
            "storage_provider_profile_github_repo_required",
            "GitHub configuration requires repo.",
            "storage_provider_profile_github_repo_invalid",
            "GitHub repo contains invalid characters.");
    }

    public static string NormalizePathPrefix(string? pathPrefix)
    {
        if (string.IsNullOrWhiteSpace(pathPrefix))
        {
            return string.Empty;
        }

        var normalized = pathPrefix.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        if (normalized.Contains("//", StringComparison.Ordinal)
            || normalized.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new ValidationException(
                "storage_provider_profile_github_path_prefix_invalid",
                "GitHub path prefix cannot contain relative traversal segments.");
        }

        return normalized;
    }

    public static string NormalizeVisibilityPolicy(string? visibilityPolicy, params string[] allowedValues)
    {
        var effectiveAllowedValues = allowedValues
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (effectiveAllowedValues.Length == 0)
        {
            throw new InvalidOperationException("At least one visibility policy must be allowed.");
        }

        var normalized = string.IsNullOrWhiteSpace(visibilityPolicy)
            ? effectiveAllowedValues[0]
            : visibilityPolicy.Trim().ToLowerInvariant();

        if (!effectiveAllowedValues.Any(value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException(
                "storage_provider_profile_github_visibility_policy_invalid",
                $"Unsupported visibilityPolicy '{normalized}'. Allowed values: {string.Join(", ", effectiveAllowedValues)}.");
        }

        return normalized;
    }

    public static string? NormalizeOptionalToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalized = token.Trim();
        if (normalized.Length < 8)
        {
            throw new ValidationException(
                "storage_provider_profile_github_token_invalid",
                "GitHub token must be at least 8 characters when provided.");
        }

        return normalized;
    }

    private static string NormalizeGitHubSegment(
        string? value,
        string requiredCode,
        string requiredMessage,
        string invalidCode,
        string invalidMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(requiredCode, requiredMessage);
        }

        var normalized = value.Trim();
        if (!GitHubSegmentRegex().IsMatch(normalized))
        {
            throw new ValidationException(invalidCode, invalidMessage);
        }

        return normalized;
    }
}

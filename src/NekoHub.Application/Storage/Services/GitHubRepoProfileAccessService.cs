using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Storage.Dtos;
using NekoHub.Application.Storage.Validation;
using NekoHub.Domain.Storage;

namespace NekoHub.Application.Storage.Services;

public sealed class GitHubRepoProfileAccessService(
    IStorageProviderProfileRepository storageProviderProfileRepository,
    IGitHubRepoProfileStorageInvoker gitHubRepoProfileStorageInvoker,
    IEnumerable<IStorageProviderProfileConfigurationValidator> validators) : IGitHubRepoProfileAccessService
{
    private const string DefaultVisibilityPolicy = "public-only";
    private const string DefaultApiBaseUrl = "https://api.github.com";
    private const string DefaultRawBaseUrl = "https://raw.githubusercontent.com";
    private const string BrowseTypeAll = "all";
    private const string BrowseTypeFile = "file";
    private const string BrowseTypeDirectory = "dir";
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private readonly IStorageProviderProfileConfigurationValidator _githubRepoValidator = validators
        .SingleOrDefault(static validator =>
            string.Equals(validator.ProviderType, StorageProviderTypes.GitHubRepo, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("github-repo configuration validator is not registered.");

    public async Task<GitHubRepoBrowseProfileResultDto> BrowseAsync(
        Guid profileId,
        GitHubRepoBrowseProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var profile = await ResolveProfileAsync(profileId, cancellationToken);
        var context = BuildContext(profile);

        var requestedPath = string.IsNullOrWhiteSpace(request.Path)
            ? null
            : request.Path.Trim();
        var recursive = request.Recursive ?? false;
        var maxDepth = request.MaxDepth ?? 2;
        var type = NormalizeBrowseType(request.Type);
        var keyword = NormalizeKeyword(request.Keyword);
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);

        var entries = await gitHubRepoProfileStorageInvoker.BrowseAsync(
            context,
            requestedPath,
            recursive,
            maxDepth,
            cancellationToken);

        var filtered = entries
            .OrderByDescending(static entry => entry.IsDirectory)
            .ThenBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Where(entry => MatchesType(entry, type))
            .Where(entry => MatchesKeyword(entry, keyword))
            .ToList();

        var total = filtered.Count;
        var skip = (page - 1) * pageSize;
        var paged = skip >= total
            ? []
            : filtered.Skip(skip).Take(pageSize).ToList();
        var hasMore = skip + paged.Count < total;

        return new GitHubRepoBrowseProfileResultDto(
            ProfileId: profile.Id,
            RequestedPath: requestedPath ?? string.Empty,
            Recursive: recursive,
            MaxDepth: maxDepth,
            Type: type,
            Keyword: keyword,
            Total: total,
            Page: page,
            PageSize: pageSize,
            HasMore: hasMore,
            VisibilityPolicy: context.VisibilityPolicy,
            UsesControlledRead: IsControlledRead(context.VisibilityPolicy),
            Items: paged.Select(ToBrowseEntry).ToList());
    }

    public async Task<GitHubRepoUpsertProfileResultDto> UpsertAsync(
        Guid profileId,
        GitHubRepoUpsertProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var profile = await ResolveProfileAsync(profileId, cancellationToken);
        var context = BuildContext(profile);

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new ValidationException(
                "storage_provider_upsert_path_required",
                "Path is required for github-repo upsert.");
        }

        if (string.IsNullOrWhiteSpace(request.ContentBase64))
        {
            throw new ValidationException(
                "storage_provider_upsert_content_required",
                "ContentBase64 is required for github-repo upsert.");
        }

        byte[] contentBytes;
        try
        {
            contentBytes = Convert.FromBase64String(request.ContentBase64.Trim());
        }
        catch (FormatException)
        {
            throw new ValidationException(
                "storage_provider_upsert_content_base64_invalid",
                "ContentBase64 must be valid base64.");
        }

        if (contentBytes.Length == 0)
        {
            throw new ValidationException(
                "storage_provider_upsert_content_empty",
                "ContentBase64 cannot be empty.");
        }

        await using var content = new MemoryStream(contentBytes, writable: false);
        var result = await gitHubRepoProfileStorageInvoker.UpsertAsync(
            context,
            content,
            new GitHubRepoUpsertFileRequest(
                RelativePath: request.Path.Trim(),
                CommitMessage: string.IsNullOrWhiteSpace(request.CommitMessage) ? null : request.CommitMessage.Trim(),
                ExpectedSha: string.IsNullOrWhiteSpace(request.ExpectedSha) ? null : request.ExpectedSha.Trim()),
            cancellationToken);

        return new GitHubRepoUpsertProfileResultDto(
            ProfileId: profile.Id,
            Path: result.RelativePath,
            Operation: result.Created ? "created" : "updated",
            Size: contentBytes.LongLength,
            Sha: result.Sha,
            VisibilityPolicy: context.VisibilityPolicy,
            UsesControlledRead: IsControlledRead(context.VisibilityPolicy),
            PublicUrl: result.PublicUrl);
    }

    private async Task<StorageProviderProfile> ResolveProfileAsync(Guid profileId, CancellationToken cancellationToken)
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

        if (!string.Equals(profile.ProviderType, StorageProviderTypes.GitHubRepo, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "storage_provider_profile_provider_type_mismatch",
                $"Storage provider profile '{profileId}' is not github-repo.");
        }

        return profile;
    }

    private GitHubRepoProfileStorageContext BuildContext(StorageProviderProfile profile)
    {
        var validated = _githubRepoValidator.Validate(profile.ConfigurationJson, profile.SecretConfigurationJson);
        var configuration = StorageProviderProfileJson.DeserializeRequiredObject<GitHubRepoProfileConfiguration>(
            validated.ConfigurationJson,
            "storage_provider_profile_configuration_invalid",
            "github-repo profile configuration is invalid.");
        var secret = StorageProviderProfileJson.DeserializeRequiredObject<GitHubRepoProfileSecretConfiguration>(
            string.IsNullOrWhiteSpace(validated.SecretConfigurationJson) ? "{}" : validated.SecretConfigurationJson,
            "storage_provider_profile_secret_configuration_invalid",
            "github-repo profile secretConfiguration is invalid.");

        var owner = NormalizeRequired(
            configuration.Owner,
            "storage_provider_profile_github_owner_required",
            "github-repo profile owner is required.");
        var repo = NormalizeRequired(
            configuration.Repo,
            "storage_provider_profile_github_repo_required",
            "github-repo profile repo is required.");
        var @ref = NormalizeRequired(
            configuration.Ref,
            "storage_provider_profile_github_repo_ref_required",
            "github-repo profile ref is required.");
        var visibilityPolicy = string.IsNullOrWhiteSpace(configuration.VisibilityPolicy)
            ? DefaultVisibilityPolicy
            : configuration.VisibilityPolicy.Trim();

        return new GitHubRepoProfileStorageContext(
            Owner: owner,
            Repo: repo,
            Ref: @ref,
            BasePath: string.IsNullOrWhiteSpace(configuration.BasePath) ? null : configuration.BasePath.Trim(),
            ApiBaseUrl: string.IsNullOrWhiteSpace(configuration.ApiBaseUrl) ? DefaultApiBaseUrl : configuration.ApiBaseUrl.Trim(),
            RawBaseUrl: string.IsNullOrWhiteSpace(configuration.RawBaseUrl) ? DefaultRawBaseUrl : configuration.RawBaseUrl.Trim(),
            VisibilityPolicy: visibilityPolicy,
            Token: string.IsNullOrWhiteSpace(secret.Token) ? null : secret.Token.Trim());
    }

    private static GitHubRepoBrowseProfileEntryDto ToBrowseEntry(GitHubRepoDirectoryEntry entry)
    {
        return new GitHubRepoBrowseProfileEntryDto(
            Name: entry.Name,
            Path: entry.RelativePath,
            Type: entry.IsDirectory ? "dir" : "file",
            IsDirectory: entry.IsDirectory,
            IsFile: !entry.IsDirectory,
            Size: entry.Size,
            Sha: entry.Sha,
            PublicUrl: entry.PublicUrl);
    }

    private static bool IsControlledRead(string visibilityPolicy)
    {
        return string.Equals(visibilityPolicy, "private-token", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBrowseType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return BrowseTypeAll;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            BrowseTypeAll => BrowseTypeAll,
            BrowseTypeFile => BrowseTypeFile,
            BrowseTypeDirectory => BrowseTypeDirectory,
            _ => throw new ValidationException(
                "storage_provider_directory_type_invalid",
                $"Unsupported browse type '{normalized}'. Allowed values: all, file, dir.")
        };
    }

    private static string? NormalizeKeyword(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static int NormalizePage(int? value)
    {
        var page = value ?? DefaultPage;
        if (page < 1)
        {
            throw new ValidationException(
                "storage_provider_directory_page_invalid",
                "Page must be greater than or equal to 1.");
        }

        return page;
    }

    private static int NormalizePageSize(int? value)
    {
        var pageSize = value ?? DefaultPageSize;
        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ValidationException(
                "storage_provider_directory_page_size_invalid",
                $"PageSize must be between 1 and {MaxPageSize}.");
        }

        return pageSize;
    }

    private static bool MatchesType(GitHubRepoDirectoryEntry entry, string type)
    {
        return type switch
        {
            BrowseTypeFile => !entry.IsDirectory,
            BrowseTypeDirectory => entry.IsDirectory,
            _ => true
        };
    }

    private static bool MatchesKeyword(GitHubRepoDirectoryEntry entry, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return entry.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || entry.RelativePath.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRequired(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(code, message);
        }

        return value.Trim();
    }

    private sealed record GitHubRepoProfileConfiguration(
        string? Owner,
        string? Repo,
        string? Ref,
        string? BasePath,
        string? VisibilityPolicy,
        string? ApiBaseUrl,
        string? RawBaseUrl);

    private sealed record GitHubRepoProfileSecretConfiguration(
        string? Token);
}

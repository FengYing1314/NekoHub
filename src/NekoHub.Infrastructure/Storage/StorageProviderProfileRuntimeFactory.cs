using System.Text.Json;
using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Options;
using NekoHub.Infrastructure.Storage.GitHub;
using NekoHub.Infrastructure.Storage.Local;
using NekoHub.Infrastructure.Storage.S3;

namespace NekoHub.Infrastructure.Storage;

public sealed class StorageProviderProfileRuntimeFactory(
    IOptions<StorageOptions> storageOptions,
    IHttpClientFactory httpClientFactory) : IStorageProviderProfileRuntimeFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly StorageOptions _storageOptions = storageOptions.Value;

    public AssetStorageLease CreateStorageLease(StorageProviderProfile profile)
    {
        return profile.ProviderType switch
        {
            StorageProviderTypes.Local => AssetStorageLease.Owned(CreateLocalStorage(profile)),
            StorageProviderTypes.S3Compatible => AssetStorageLease.Owned(CreateS3Storage(profile)),
            StorageProviderTypes.GitHubRepo => AssetStorageLease.Owned(CreateGitHubRepoStorage(profile)),
            _ => throw new InvalidOperationException(
                $"Provider type '{profile.ProviderType}' does not have a runtime asset storage implementation.")
        };
    }

    public StorageProviderRuntimeDescriptor Describe(StorageProviderProfile profile)
    {
        var capabilities = profile.GetCapabilities();

        return profile.ProviderType switch
        {
            StorageProviderTypes.Local => new StorageProviderRuntimeDescriptor(
                ProviderType: profile.ProviderType,
                ProviderName: StorageProviderExtensions.LocalProviderName,
                Capabilities: capabilities),
            StorageProviderTypes.S3Compatible => new StorageProviderRuntimeDescriptor(
                ProviderType: profile.ProviderType,
                ProviderName: GetS3ProviderName(profile.ConfigurationJson),
                Capabilities: capabilities),
            StorageProviderTypes.GitHubRepo => new StorageProviderRuntimeDescriptor(
                ProviderType: profile.ProviderType,
                ProviderName: StorageProviderExtensions.GitHubRepoProviderName,
                Capabilities: capabilities),
            _ => new StorageProviderRuntimeDescriptor(
                ProviderType: profile.ProviderType,
                ProviderName: profile.ProviderType,
                Capabilities: capabilities)
        };
    }

    private LocalAssetStorage CreateLocalStorage(StorageProviderProfile profile)
    {
        var configuration = DeserializeRequired<LocalStorageProfileConfiguration>(
            profile.ConfigurationJson,
            $"Storage provider profile '{profile.Id}' local configuration is invalid.");

        if (string.IsNullOrWhiteSpace(configuration.RootPath))
        {
            throw new InvalidOperationException(
                $"Storage provider profile '{profile.Id}' local configuration requires rootPath.");
        }

        var localOptions = new LocalStorageOptions
        {
            RootPath = configuration.RootPath.Trim(),
            CreateDirectoryIfMissing = configuration.CreateDirectoryIfMissing
        };

        return new LocalAssetStorage(
            Microsoft.Extensions.Options.Options.Create(localOptions),
            Microsoft.Extensions.Options.Options.Create(CreateStorageOptions(
                providerName: StorageProviderExtensions.LocalProviderName,
                publicBaseUrl: configuration.PublicBaseUrl)));
    }

    private S3AssetStorage CreateS3Storage(StorageProviderProfile profile)
    {
        var configuration = DeserializeRequired<S3StorageProfileConfiguration>(
            profile.ConfigurationJson,
            $"Storage provider profile '{profile.Id}' S3 configuration is invalid.");
        var secret = DeserializeRequired<S3StorageSecretConfiguration>(
            string.IsNullOrWhiteSpace(profile.SecretConfigurationJson) ? "{}" : profile.SecretConfigurationJson,
            $"Storage provider profile '{profile.Id}' S3 secret configuration is invalid.");

        var providerName = string.IsNullOrWhiteSpace(configuration.ProviderName)
            ? S3StorageOptions.DefaultProviderName
            : configuration.ProviderName.Trim();

        var s3Options = new S3StorageOptions
        {
            ProviderName = providerName,
            Endpoint = NormalizeRequired(configuration.Endpoint, $"Storage provider profile '{profile.Id}' S3 endpoint is required."),
            Bucket = NormalizeRequired(configuration.Bucket, $"Storage provider profile '{profile.Id}' S3 bucket is required."),
            Region = NormalizeRequired(configuration.Region, $"Storage provider profile '{profile.Id}' S3 region is required."),
            AccessKey = NormalizeRequired(secret.AccessKey, $"Storage provider profile '{profile.Id}' S3 accessKey is required."),
            SecretKey = NormalizeRequired(secret.SecretKey, $"Storage provider profile '{profile.Id}' S3 secretKey is required."),
            ForcePathStyle = configuration.ForcePathStyle,
            PublicBaseUrl = NormalizeOptional(configuration.PublicBaseUrl)
        };

        return new S3AssetStorage(
            Microsoft.Extensions.Options.Options.Create(s3Options),
            Microsoft.Extensions.Options.Options.Create(CreateStorageOptions(
                providerName: providerName,
                publicBaseUrl: configuration.PublicBaseUrl)));
    }

    private GitHubRepoAssetStorage CreateGitHubRepoStorage(StorageProviderProfile profile)
    {
        var configuration = DeserializeRequired<GitHubRepoProfileConfiguration>(
            profile.ConfigurationJson,
            $"Storage provider profile '{profile.Id}' github-repo configuration is invalid.");
        var secret = DeserializeRequired<GitHubRepoSecretConfiguration>(
            string.IsNullOrWhiteSpace(profile.SecretConfigurationJson) ? "{}" : profile.SecretConfigurationJson,
            $"Storage provider profile '{profile.Id}' github-repo secret configuration is invalid.");

        var options = new GitHubRepoStorageOptions
        {
            ProviderName = GitHubRepoStorageOptions.DefaultProviderName,
            Owner = NormalizeRequired(configuration.Owner, $"Storage provider profile '{profile.Id}' github-repo owner is required."),
            Repo = NormalizeRequired(configuration.Repo, $"Storage provider profile '{profile.Id}' github-repo repo is required."),
            Ref = NormalizeRequired(configuration.Ref, $"Storage provider profile '{profile.Id}' github-repo ref is required."),
            BasePath = NormalizeOptional(configuration.BasePath),
            ApiBaseUrl = NormalizeOptional(configuration.ApiBaseUrl) ?? GitHubRepoStorageOptions.DefaultApiBaseUrl,
            RawBaseUrl = NormalizeOptional(configuration.RawBaseUrl) ?? GitHubRepoStorageOptions.DefaultRawBaseUrl,
            VisibilityPolicy = NormalizeOptional(configuration.VisibilityPolicy) ?? "public-only",
            Token = NormalizeOptional(secret.Token)
        };

        return new GitHubRepoAssetStorage(Microsoft.Extensions.Options.Options.Create(options), httpClientFactory);
    }

    private StorageOptions CreateStorageOptions(string providerName, string? publicBaseUrl)
    {
        return new StorageOptions
        {
            Provider = providerName,
            PublicBaseUrl = string.IsNullOrWhiteSpace(_storageOptions.PublicBaseUrl)
                ? NormalizeOptional(publicBaseUrl)
                : _storageOptions.PublicBaseUrl
        };
    }

    private static string GetS3ProviderName(string configurationJson)
    {
        var configuration = DeserializeRequired<S3StorageProfileConfiguration>(
            configurationJson,
            "S3 profile runtime descriptor configuration is invalid.");

        return string.IsNullOrWhiteSpace(configuration.ProviderName)
            ? S3StorageOptions.DefaultProviderName
            : configuration.ProviderName.Trim();
    }

    private static T DeserializeRequired<T>(string json, string errorMessage)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                throw new InvalidOperationException(errorMessage);
            }

            var model = JsonSerializer.Deserialize<T>(document.RootElement.GetRawText(), SerializerOptions);
            return model ?? throw new InvalidOperationException(errorMessage);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed record LocalStorageProfileConfiguration(
        string? RootPath,
        bool CreateDirectoryIfMissing = true,
        string? PublicBaseUrl = null);

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

    private sealed record GitHubRepoProfileConfiguration(
        string? Owner,
        string? Repo,
        string? Ref,
        string? BasePath,
        string? VisibilityPolicy,
        string? ApiBaseUrl,
        string? RawBaseUrl);

    private sealed record GitHubRepoSecretConfiguration(
        string? Token);
}

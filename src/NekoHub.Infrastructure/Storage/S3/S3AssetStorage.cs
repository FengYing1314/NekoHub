using System.Net;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Infrastructure.Options;

namespace NekoHub.Infrastructure.Storage.S3;

public sealed class S3AssetStorage : IAssetStorage, IDisposable
{
    private readonly S3StorageOptions _s3StorageOptions;
    private readonly Lazy<IAmazonS3> _lazyClient;

    public S3AssetStorage(IOptions<S3StorageOptions> s3StorageOptions)
    {
        _s3StorageOptions = s3StorageOptions.Value;
        _lazyClient = new Lazy<IAmazonS3>(CreateClient, isThreadSafe: true);
    }

    public string ProviderName => _s3StorageOptions.ProviderName;

    public async Task<StoredAssetObject> StoreAsync(
        Stream content,
        StoreAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var storageKey = BuildStorageKey(request.Extension);
        var putRequest = new PutObjectRequest
        {
            BucketName = _s3StorageOptions.Bucket,
            Key = storageKey,
            InputStream = content,
            ContentType = request.ContentType,
            AutoCloseStream = false
        };

        await _lazyClient.Value.PutObjectAsync(putRequest, cancellationToken);

        return new StoredAssetObject(
            Provider: ProviderName,
            StorageKey: storageKey,
            PublicUrl: BuildPublicUrl(storageKey),
            StoredFileName: Path.GetFileName(storageKey));
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        try
        {
            using var response = await _lazyClient.Value.GetObjectAsync(_s3StorageOptions.Bucket, storageKey, cancellationToken);
            var memory = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memory, cancellationToken);
            memory.Position = 0;
            return memory;
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return null;
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = _s3StorageOptions.Bucket,
            Key = storageKey
        };

        await _lazyClient.Value.DeleteObjectAsync(deleteRequest, cancellationToken);
    }

    public Task<string?> GetPublicUrlAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        return Task.FromResult(BuildPublicUrl(storageKey));
    }

    public void Dispose()
    {
        if (_lazyClient.IsValueCreated)
        {
            _lazyClient.Value.Dispose();
        }
    }

    private IAmazonS3 CreateClient()
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = _s3StorageOptions.ForcePathStyle
        };

        if (!string.IsNullOrWhiteSpace(_s3StorageOptions.Endpoint))
        {
            config.ServiceURL = _s3StorageOptions.Endpoint;
            config.AuthenticationRegion = string.IsNullOrWhiteSpace(_s3StorageOptions.Region)
                ? null
                : _s3StorageOptions.Region;
        }
        else if (!string.IsNullOrWhiteSpace(_s3StorageOptions.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(_s3StorageOptions.Region);
        }

        return new AmazonS3Client(
            awsAccessKeyId: _s3StorageOptions.AccessKey,
            awsSecretAccessKey: _s3StorageOptions.SecretKey,
            clientConfig: config);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_s3StorageOptions.ProviderName))
        {
            throw new InvalidOperationException("Storage:S3:ProviderName is required.");
        }

        if (string.IsNullOrWhiteSpace(_s3StorageOptions.Endpoint))
        {
            throw new InvalidOperationException("Storage:S3:Endpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(_s3StorageOptions.Bucket))
        {
            throw new InvalidOperationException("Storage:S3:Bucket is required.");
        }

        if (string.IsNullOrWhiteSpace(_s3StorageOptions.AccessKey))
        {
            throw new InvalidOperationException("Storage:S3:AccessKey is required.");
        }

        if (string.IsNullOrWhiteSpace(_s3StorageOptions.SecretKey))
        {
            throw new InvalidOperationException("Storage:S3:SecretKey is required.");
        }
    }

    private string BuildStorageKey(string extension)
    {
        var safeExtension = NormalizeExtension(extension);
        return $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.CreateVersion7():N}{safeExtension}";
    }

    private string? BuildPublicUrl(string storageKey)
    {
        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        var encodedKey = string.Join(
            '/',
            normalizedKey
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        if (!string.IsNullOrWhiteSpace(_s3StorageOptions.PublicBaseUrl))
        {
            return $"{_s3StorageOptions.PublicBaseUrl.TrimEnd('/')}/{encodedKey}";
        }

        if (!Uri.TryCreate(_s3StorageOptions.Endpoint, UriKind.Absolute, out var endpointUri))
        {
            return null;
        }

        var bucket = _s3StorageOptions.Bucket!.Trim();
        if (_s3StorageOptions.ForcePathStyle)
        {
            return $"{endpointUri.Scheme}://{endpointUri.Authority}/{Uri.EscapeDataString(bucket)}/{encodedKey}";
        }

        var builder = new UriBuilder(endpointUri)
        {
            Host = $"{bucket}.{endpointUri.Host}",
            Path = encodedKey
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private static bool IsNotFound(AmazonS3Exception exception)
    {
        if (string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return exception.StatusCode == HttpStatusCode.NotFound
               && !string.Equals(exception.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.StartsWith('.') ? extension : $".{extension}";
        return normalized.ToLowerInvariant();
    }
}

namespace NekoHub.Infrastructure.Options;

public sealed class S3StorageOptions
{
    public const string DefaultProviderName = "s3";
    public const string SectionName = "Storage:S3";

    public string ProviderName { get; init; } = DefaultProviderName;

    public string? Endpoint { get; init; }

    public string? Bucket { get; init; }

    public string? Region { get; init; }

    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }

    public bool ForcePathStyle { get; init; } = true;

    public string? PublicBaseUrl { get; init; }
}

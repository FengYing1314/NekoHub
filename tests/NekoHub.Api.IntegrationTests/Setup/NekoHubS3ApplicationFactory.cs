using NekoHub.Infrastructure.Options;

namespace NekoHub.Api.IntegrationTests.Setup;

public sealed class NekoHubS3ApplicationFactory(MinioContainerFixture minioContainerFixture) : NekoHubApplicationFactory
{
    protected override IDictionary<string, string?> CreateInMemoryConfiguration()
    {
        var config = base.CreateInMemoryConfiguration();
        config["Storage:Provider"] = S3StorageOptions.DefaultProviderName;
        config["Storage:S3:ProviderName"] = S3StorageOptions.DefaultProviderName;
        config["Storage:S3:Endpoint"] = minioContainerFixture.Endpoint;
        config["Storage:S3:Bucket"] = minioContainerFixture.BucketName;
        config["Storage:S3:Region"] = "us-east-1";
        config["Storage:S3:AccessKey"] = minioContainerFixture.AccessKey;
        config["Storage:S3:SecretKey"] = minioContainerFixture.SecretKey;
        config["Storage:S3:ForcePathStyle"] = "true";
        config["Storage:S3:PublicBaseUrl"] = $"{minioContainerFixture.Endpoint}/{minioContainerFixture.BucketName}";
        return config;
    }
}

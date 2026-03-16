using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Setup;

public sealed class MinioContainerFixture : IAsyncLifetime
{
    public const string RunS3IntegrationEnv = "NEKOHUB_RUN_S3_IT";
    private const int MinioApiPort = 9000;
    private IContainer? _container;

    public string AccessKey { get; } = "minioadmin";

    public string SecretKey { get; } = "minioadmin";

    public string BucketName { get; } = "nekohub-it";

    public string Endpoint { get; private set; } = string.Empty;

    public bool IsAvailable { get; private set; }

    public string? UnavailableReason { get; private set; }

    public bool IsEnabled { get; private set; }

    public async Task InitializeAsync()
    {
        IsEnabled = IsS3IntegrationEnabled();
        if (!IsEnabled)
        {
            IsAvailable = false;
            UnavailableReason = $"{RunS3IntegrationEnv} 未启用。";
            return;
        }

        try
        {
            _container = BuildContainer();
            await _container.StartAsync();
            Endpoint = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(MinioApiPort)}";
            await EnsureBucketReadyAsync();
            IsAvailable = true;
        }
        catch (Exception exception)
        {
            IsEnabled = false;
            IsAvailable = false;
            UnavailableReason = $"MinIO 启动失败: {exception.Message}";
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }

    private IContainer BuildContainer()
    {
        return new ContainerBuilder("minio/minio:RELEASE.2025-02-03T21-03-04Z")
            .WithName($"nekohub-minio-tests-{Guid.NewGuid():N}")
            .WithPortBinding(MinioApiPort, true)
            .WithPortBinding(9001, true)
            .WithEnvironment("MINIO_ROOT_USER", AccessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
            .WithCommand("server", "/data", "--address", ":9000", "--console-address", ":9001")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(MinioApiPort))
            .WithCleanUp(true)
            .Build();
    }

    private async Task EnsureBucketReadyAsync()
    {
        using var client = CreateS3Client();
        await client.PutBucketAsync(new PutBucketRequest
        {
            BucketName = BucketName
        });

        // 允许匿名读，便于 content redirect 的 URL 在测试中具备可访问语义。
        var policy = $$"""
                       {
                         "Version": "2012-10-17",
                         "Statement": [
                           {
                             "Effect": "Allow",
                             "Principal": { "AWS": ["*"] },
                             "Action": ["s3:GetObject"],
                             "Resource": ["arn:aws:s3:::{{BucketName}}/*"]
                           }
                         ]
                       }
                       """;

        await client.PutBucketPolicyAsync(new PutBucketPolicyRequest
        {
            BucketName = BucketName,
            Policy = policy
        });
    }

    private AmazonS3Client CreateS3Client()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = Endpoint,
            ForcePathStyle = true
        };

        return new AmazonS3Client(AccessKey, SecretKey, config);
    }

    private static bool IsS3IntegrationEnabled()
    {
        var value = Environment.GetEnvironmentVariable(RunS3IntegrationEnv);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}

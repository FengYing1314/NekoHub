using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class S3AssetLifecycleTests : IClassFixture<MinioContainerFixture>
{
    private readonly MinioContainerFixture _minio;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public S3AssetLifecycleTests(MinioContainerFixture minio)
    {
        _minio = minio;
    }

    [Fact]
    public async Task S3_Asset_Complete_Lifecycle_Test()
    {
        if (!_minio.IsEnabled)
        {
            return;
        }

        EnsureMinioAvailableOrThrow();

        using var factory = new NekoHubS3ApplicationFactory(_minio);
        using var client = factory.CreateClient();
        using var noRedirectClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var uploadResponse = await UploadTestImage(client, "s3-test.png", "image/png", new byte[100]);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        asset.Should().NotBeNull();
        asset!.StorageProvider.Should().Be("s3");
        asset.IsPublic.Should().BeTrue();
        asset.PublicUrl.Should().StartWith("http://test-server/content/");

        var detailResponse = await client.GetAsync($"/api/v1/assets/{asset.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail!.Id.Should().Be(asset.Id);
        detail.StorageProvider.Should().Be("s3");

        var contentResponse = await noRedirectClient.GetAsync($"/api/v1/assets/{asset.Id}/content");
        contentResponse.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);
        contentResponse.Headers.Location.Should().NotBeNull();
        contentResponse.Headers.Location!.ToString().Should().StartWith("http://test-server/content/");

        var deleteResponse = await client.DeleteAsync($"/api/v1/assets/{asset.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterDeleteDetail = await client.GetAsync($"/api/v1/assets/{asset.Id}");
        afterDeleteDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var afterDeleteError = await GetErrorAsync(afterDeleteDetail);
        afterDeleteError!.Code.Should().Be("asset_not_found");

        var afterDeleteContent = await client.GetAsync($"/api/v1/assets/{asset.Id}/content");
        afterDeleteContent.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task S3_Default_Profile_Should_Work_Without_Global_S3_Runtime_Config()
    {
        if (!_minio.IsEnabled)
        {
            return;
        }

        EnsureMinioAvailableOrThrow();

        using var factory = new NekoHubApplicationFactory();
        var profileId = await SeedDefaultS3ProfileAsync(factory);
        using var client = factory.CreateClient();

        var content = new byte[] { 9, 8, 7, 6, 5, 4 };
        var uploadResponse = await UploadTestImage(client, "profile-s3.png", "image/png", content);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        asset.Should().NotBeNull();
        asset!.StorageProvider.Should().Be("s3");
        asset.StorageProviderProfileId.Should().Be(profileId);

        var directContentResponse = await client.GetAsync($"/content/{asset.StorageKey}");
        directContentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloaded = await directContentResponse.Content.ReadAsByteArrayAsync();
        downloaded.Should().Equal(content);
    }

    [Fact]
    public async Task S3_Default_Profile_Should_Generate_Thumbnail_Derivative()
    {
        if (!_minio.IsEnabled)
        {
            return;
        }

        EnsureMinioAvailableOrThrow();

        using var factory = new NekoHubApplicationFactory();
        var profileId = await SeedDefaultS3ProfileAsync(factory);
        using var client = factory.CreateClient();
        using var noRedirectClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var uploadResponse = await UploadTestImage(client, "thumb-s3.png", "image/png", CreatePngBytes(2, 2));
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        asset.Should().NotBeNull();
        asset!.StorageProviderProfileId.Should().Be(profileId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        var derivative = await dbContext.AssetDerivatives
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.SourceAssetId == asset.Id && x.Kind == AssetDerivativeKinds.Thumbnail256);

        derivative.Should().NotBeNull();
        derivative!.StorageProvider.Should().Be("s3");
        derivative.StorageKey.Should().NotBeNullOrWhiteSpace();

        var derivativeResponse = await noRedirectClient.GetAsync($"/content/{derivative.StorageKey}");
        derivativeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        derivativeResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await derivativeResponse.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }

    private void EnsureMinioAvailableOrThrow()
    {
        if (_minio.IsAvailable)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{MinioContainerFixture.RunS3IntegrationEnv} 已启用，但 MinIO 不可用: {_minio.UnavailableReason}");
    }

    private async Task<HttpResponseMessage> UploadTestImage(HttpClient client, string fileName, string contentType, byte[] content)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        requestContent.Add(fileContent, "File", fileName);

        return await client.PostAsync("/api/v1/assets", requestContent);
    }

    private async Task<Guid> SeedDefaultS3ProfileAsync(NekoHubApplicationFactory factory)
    {
        var profile = new StorageProviderProfile(
            id: Guid.CreateVersion7(),
            name: $"s3-default-{Guid.CreateVersion7():N}",
            providerType: StorageProviderTypes.S3Compatible,
            configurationJson: $$"""
                                {
                                  "providerName": "s3",
                                  "endpoint": "{{_minio.Endpoint}}",
                                  "bucket": "{{_minio.BucketName}}",
                                  "region": "us-east-1",
                                  "forcePathStyle": true
                                }
                                """,
            capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.S3Compatible),
            isEnabled: true,
            isDefault: true,
            secretConfigurationJson: $$"""
                                      {
                                        "accessKey": "{{_minio.AccessKey}}",
                                        "secretKey": "{{_minio.SecretKey}}"
                                      }
                                      """);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        dbContext.StorageProviderProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return profile.Id;
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(80, 140, 255, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }

    private async Task<T?> GetResponseDataAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions);
        return content != null ? content.Data : default;
    }

    private async Task<ApiError?> GetErrorAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(_jsonOptions);
        return content?.Error;
    }
}

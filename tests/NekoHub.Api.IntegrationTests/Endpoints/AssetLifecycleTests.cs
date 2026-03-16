using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AssetLifecycleTests : IntegrationTestBase
{
    public AssetLifecycleTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Upload_Invalid_ContentType_Should_Fail()
    {
        var response = await UploadTestImage("test.txt", "text/plain", new byte[10]);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Upload_Too_Large_File_Should_Fail()
    {
        // Testing 配置默认 10 MB，上浮 1 MB 足够触发边界且不会让测试负担过大。
        var largeContent = new byte[11 * 1024 * 1024];
        var response = await UploadTestImage("huge.png", "image/png", largeContent);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error!.Code.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Get_NonExistent_Asset_Should_Return_404()
    {
        var response = await Client.GetAsync($"/api/v1/assets/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await GetErrorAsync(response);
        error!.Code.Should().Be("asset_not_found");
    }

    [Fact]
    public async Task Upload_Checksum_Should_Be_Stable_And_Queryable()
    {
        var sameContentA = new byte[] { 1, 2, 3, 4, 5, 6 };
        var sameContentB = new byte[] { 1, 2, 3, 4, 5, 6 };
        var differentContent = new byte[] { 9, 8, 7, 6, 5, 4 };

        var upload1 = await UploadTestImage("checksum-a.png", "image/png", sameContentA);
        upload1.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset1 = await GetResponseDataAsync<AssetResponse>(upload1);
        asset1.Should().NotBeNull();
        asset1!.ChecksumSha256.Should().NotBeNullOrWhiteSpace();
        Regex.IsMatch(asset1.ChecksumSha256!, "^[0-9a-f]{64}$").Should().BeTrue();

        var detail1 = await Client.GetAsync($"/api/v1/assets/{asset1.Id}");
        detail1.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailData1 = await GetResponseDataAsync<AssetResponse>(detail1);
        detailData1!.ChecksumSha256.Should().Be(asset1.ChecksumSha256);

        var upload2 = await UploadTestImage("checksum-b.png", "image/png", sameContentB);
        upload2.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset2 = await GetResponseDataAsync<AssetResponse>(upload2);
        asset2!.ChecksumSha256.Should().Be(asset1.ChecksumSha256);

        var upload3 = await UploadTestImage("checksum-c.png", "image/png", differentContent);
        upload3.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset3 = await GetResponseDataAsync<AssetResponse>(upload3);
        asset3!.ChecksumSha256.Should().NotBe(asset1.ChecksumSha256);

        await Client.DeleteAsync($"/api/v1/assets/{asset1.Id}");
        await Client.DeleteAsync($"/api/v1/assets/{asset2.Id}");
        await Client.DeleteAsync($"/api/v1/assets/{asset3.Id}");
    }

    [Fact]
    public async Task Upload_Real_Image_Should_Extract_Width_And_Height()
    {
        var png1x1 = CreatePngBytes(1, 1);

        var upload = await UploadTestImage("pixel.png", "image/png", png1x1);
        upload.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asset = await GetResponseDataAsync<AssetResponse>(upload);
        asset.Should().NotBeNull();
        asset!.Width.Should().Be(1);
        asset.Height.Should().Be(1);

        var detailResponse = await Client.GetAsync($"/api/v1/assets/{asset.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.Width.Should().Be(1);
        detail.Height.Should().Be(1);

        await Client.DeleteAsync($"/api/v1/assets/{asset.Id}");
    }

    [Fact]
    public async Task Upload_Image_Should_Generate_Thumbnail_Derivative()
    {
        var png1x1 = CreatePngBytes(1, 1);

        var upload = await UploadTestImage("thumb-source.png", "image/png", png1x1);
        upload.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset = await GetResponseDataAsync<AssetResponse>(upload);
        asset.Should().NotBeNull();
        var assetData = asset!;
        var assetId = assetData.Id;

        var derivative = await EventuallyAsync(async () =>
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
            return await dbContext.AssetDerivatives
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.SourceAssetId == assetId && x.Kind == AssetDerivativeKinds.Thumbnail256);
        }, item => item is not null);

        derivative.Should().NotBeNull();
        derivative!.ContentType.Should().Be("image/png");
        derivative.Extension.Should().Be(".png");
        derivative.StorageProvider.Should().Be(assetData.StorageProvider);
        derivative.Width.Should().Be(1);
        derivative.Height.Should().Be(1);
        derivative.Size.Should().BeGreaterThan(0);
        derivative.StorageKey.Should().NotBeNullOrWhiteSpace();

        var derivativePath = Path.Combine(Factory.TestStoragePath, derivative.StorageKey.Replace('\\', '/').TrimStart('/'));
        File.Exists(derivativePath).Should().BeTrue();

        var detail = await EventuallyAsync(async () =>
        {
            var detailResponse = await Client.GetAsync($"/api/v1/assets/{assetId}");
            detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            return await GetResponseDataAsync<AssetResponse>(detailResponse);
        }, item => item is not null
            && item.Derivatives.Any(d => d.Kind == AssetDerivativeKinds.Thumbnail256)
            && item.LatestExecutionSummary is not null);
        detail.Should().NotBeNull();
        detail!.Derivatives.Should().ContainSingle(d => d.Kind == AssetDerivativeKinds.Thumbnail256);
        var derivativeSummary = detail.Derivatives.Single(d => d.Kind == AssetDerivativeKinds.Thumbnail256);
        derivativeSummary.ContentType.Should().Be("image/png");
        derivativeSummary.Extension.Should().Be(".png");
        derivativeSummary.Size.Should().BeGreaterThan(0);
        derivativeSummary.Width.Should().Be(1);
        derivativeSummary.Height.Should().Be(1);
        derivativeSummary.PublicUrl.Should().NotBeNullOrWhiteSpace();
        detail.StructuredResults.Should().NotBeNull();
        detail.LatestExecutionSummary.Should().NotBeNull();
        detail.LatestExecutionSummary!.TriggerSource.Should().Be("upload");
        detail.LatestExecutionSummary.Steps.Should().NotBeEmpty();

        var deleteResponse = await Client.DeleteAsync($"/api/v1/assets/{assetId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        File.Exists(derivativePath).Should().BeFalse();
    }

    private async Task<HttpResponseMessage> UploadTestImage(string fileName, string contentType, byte[] content)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        requestContent.Add(fileContent, "File", fileName);

        return await Client.PostAsync("/api/v1/assets", requestContent);
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(255, 120, 80, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

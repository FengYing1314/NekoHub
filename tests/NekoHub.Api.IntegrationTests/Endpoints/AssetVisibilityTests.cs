using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AssetVisibilityTests : IntegrationTestBase
{
    public AssetVisibilityTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Upload_Should_Default_To_Public_And_Return_IsPublic_In_List_And_Detail()
    {
        var uploadResponse = await UploadTestPngAsync("visibility-default.png");
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        asset.Should().NotBeNull();
        asset!.IsPublic.Should().BeTrue();
        asset.PublicUrl.Should().NotBeNullOrWhiteSpace();

        var detailResponse = await Client.GetAsync($"/api/v1/assets/{asset.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.IsPublic.Should().BeTrue();
        detail.PublicUrl.Should().NotBeNullOrWhiteSpace();

        var listResponse = await Client.GetAsync("/api/v1/assets?page=1&pageSize=10");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await GetResponseDataAsync<AssetPagedResponse>(listResponse);
        paged.Should().NotBeNull();
        paged!.Items.Should().ContainSingle(item => item.Id == asset.Id && item.IsPublic);
    }

    [Fact]
    public async Task Upload_Should_Allow_Private_And_Block_Public_Content_Access()
    {
        var uploadResponse = await UploadTestPngAsync("visibility-private.png", isPublic: false);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        asset.Should().NotBeNull();
        asset!.IsPublic.Should().BeFalse();
        asset.PublicUrl.Should().BeNull();

        var detailResponse = await Client.GetAsync($"/api/v1/assets/{asset.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.IsPublic.Should().BeFalse();
        detail.PublicUrl.Should().BeNull();
        detail.Derivatives.Should().NotBeEmpty();
        detail.Derivatives.Should().AllSatisfy(derivative => derivative.PublicUrl.Should().BeNull());

        var listResponse = await Client.GetAsync("/api/v1/assets?page=1&pageSize=10");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await GetResponseDataAsync<AssetPagedResponse>(listResponse);
        paged.Should().NotBeNull();
        paged!.Items.Should().ContainSingle(item => item.Id == asset.Id && !item.IsPublic && item.PublicUrl == null);

        var contentResponse = await Client.GetAsync($"/api/v1/assets/{asset.Id}/content");
        contentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var contentError = await GetErrorAsync(contentResponse);
        contentError.Should().NotBeNull();
        contentError!.Code.Should().Be("asset_not_found");

        var directContentResponse = await Client.GetAsync(BuildPublicContentPath(asset.StorageKey));
        directContentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_Should_Toggle_IsPublic_And_Update_Public_Content_Access()
    {
        var uploadResponse = await UploadTestPngAsync("visibility-toggle.png");
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var asset = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        asset.Should().NotBeNull();
        var assetId = asset!.Id;
        var contentPath = BuildPublicContentPath(asset.StorageKey);

        var initialContentResponse = await Client.GetAsync(contentPath);
        initialContentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var privatePatchResponse = await PatchAsync(
            assetId,
            """
            {
              "isPublic": false
            }
            """);

        privatePatchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var privateAsset = await GetResponseDataAsync<AssetResponse>(privatePatchResponse);
        privateAsset.Should().NotBeNull();
        privateAsset!.IsPublic.Should().BeFalse();
        privateAsset.PublicUrl.Should().BeNull();

        var privateDetailResponse = await Client.GetAsync($"/api/v1/assets/{assetId}");
        var privateDetail = await GetResponseDataAsync<AssetResponse>(privateDetailResponse);
        privateDetail.Should().NotBeNull();
        privateDetail!.IsPublic.Should().BeFalse();
        privateDetail.PublicUrl.Should().BeNull();
        privateDetail.Derivatives.Should().AllSatisfy(derivative => derivative.PublicUrl.Should().BeNull());

        (await Client.GetAsync($"/api/v1/assets/{assetId}/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync(contentPath)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var publicPatchResponse = await PatchAsync(
            assetId,
            """
            {
              "isPublic": true
            }
            """);

        publicPatchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicAsset = await GetResponseDataAsync<AssetResponse>(publicPatchResponse);
        publicAsset.Should().NotBeNull();
        publicAsset!.IsPublic.Should().BeTrue();
        publicAsset.PublicUrl.Should().NotBeNullOrWhiteSpace();

        var publicContentResponse = await Client.GetAsync(contentPath);
        publicContentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        publicContentResponse.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
    }

    private async Task<HttpResponseMessage> UploadTestPngAsync(string fileName, bool? isPublic = null)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePngBytes(1, 1));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);

        if (isPublic.HasValue)
        {
            requestContent.Add(new StringContent(isPublic.Value ? "true" : "false"), "IsPublic");
        }

        return await Client.PostAsync("/api/v1/assets", requestContent);
    }

    private async Task<HttpResponseMessage> PatchAsync(Guid assetId, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Client.PatchAsync($"/api/v1/assets/{assetId}", content);
    }

    private static string BuildPublicContentPath(string storageKey)
    {
        var encodedSegments = storageKey
            .Replace('\\', '/')
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);

        return $"/content/{string.Join('/', encodedSegments)}";
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(120, 180, 255, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class PublicAssetsControllerTests : IntegrationTestBase
{
    public PublicAssetsControllerTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Public_List_Should_Only_Return_Public_Ready_Assets_Without_Internal_Fields()
    {
        var publicAsset = await UploadTestImageAsync("public-gallery.png", isPublic: true);
        await UploadTestImageAsync("private-gallery.png", isPublic: false);

        var response = await Client.GetAsync("/api/v1/public/assets?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<PublicAssetPagedResponse>(response);
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle(item => item.Id == publicAsset.Id);

        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().NotContain("storageKey");
        rawJson.Should().NotContain("checksumSha256");
        rawJson.Should().NotContain("storageProvider");
        rawJson.Should().NotContain("structuredResults");
        rawJson.Should().NotContain("latestExecutionSummary");
    }

    [Fact]
    public async Task Public_Detail_Should_Return_404_For_Private_Asset()
    {
        var privateAsset = await UploadTestImageAsync("private-detail.png", isPublic: false);

        var response = await Client.GetAsync($"/api/v1/public/assets/{privateAsset.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("asset_not_found");
    }

    [Fact]
    public async Task Public_Detail_Should_Return_Public_Asset_Without_Internal_Fields()
    {
        var publicAsset = await UploadTestImageAsync("public-detail.png", isPublic: true);

        var response = await EventuallyAsync(
            () => Client.GetAsync($"/api/v1/public/assets/{publicAsset.Id}"),
            detailResponse => detailResponse.StatusCode == HttpStatusCode.OK);

        var payload = await GetResponseDataAsync<PublicAssetResponse>(response);
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(publicAsset.Id);

        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().NotContain("storageKey");
        rawJson.Should().NotContain("checksumSha256");
        rawJson.Should().NotContain("storageProvider");
        rawJson.Should().NotContain("structuredResults");
        rawJson.Should().NotContain("latestExecutionSummary");
    }

    private async Task<AssetResponse> UploadTestImageAsync(string fileName, bool isPublic)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePngBytes());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);
        requestContent.Add(new StringContent(isPublic ? "true" : "false"), "IsPublic");

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var payload = await GetResponseDataAsync<AssetResponse>(response);
        payload.Should().NotBeNull();
        return payload!;
    }

    private static byte[] CreatePngBytes()
    {
        return
        [
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 0, 1, 0, 0, 0, 1,
            8, 2, 0, 0, 0, 144, 119, 83,
            222, 0, 0, 0, 12, 73, 68, 65,
            84, 8, 153, 99, 248, 207, 192, 0,
            0, 3, 1, 1, 0, 24, 221, 141,
            184, 0, 0, 0, 0, 73, 69, 78,
            68, 174, 66, 96, 130
        ];
    }
}

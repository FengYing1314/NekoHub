using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AssetMetadataPatchTests : IntegrationTestBase
{
    public AssetMetadataPatchTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Patch_Metadata_Should_Respect_Omitted_Null_And_Set_Semantics()
    {
        var asset = await UploadTestImageAsync(
            "patch-source.png",
            "image/png",
            new byte[96],
            description: "initial-description",
            altText: "initial-alt");

        var patchDescriptionResponse = await PatchAsync(
            asset.Id,
            """
            {
              "description": "updated-description"
            }
            """);

        patchDescriptionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var patchedDescription = await GetResponseDataAsync<AssetResponse>(patchDescriptionResponse);
        patchedDescription.Should().NotBeNull();
        patchedDescription!.Description.Should().Be("updated-description");
        patchedDescription.AltText.Should().Be("initial-alt");
        patchedDescription.OriginalFileName.Should().Be("patch-source.png");

        var clearResponse = await PatchAsync(
            asset.Id,
            """
            {
              "altText": null,
              "originalFileName": null
            }
            """);

        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cleared = await GetResponseDataAsync<AssetResponse>(clearResponse);
        cleared.Should().NotBeNull();
        cleared!.Description.Should().Be("updated-description");
        cleared.AltText.Should().BeNull();
        cleared.OriginalFileName.Should().BeNull();

        var setFileNameResponse = await PatchAsync(
            asset.Id,
            """
            {
              "originalFileName": "patched-name.png"
            }
            """);

        setFileNameResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamed = await GetResponseDataAsync<AssetResponse>(setFileNameResponse);
        renamed.Should().NotBeNull();
        renamed!.OriginalFileName.Should().Be("patched-name.png");
        renamed.Description.Should().Be("updated-description");
        renamed.AltText.Should().BeNull();
    }

    [Fact]
    public async Task Patch_Metadata_Should_Update_IsPublic_When_Field_Is_Specified()
    {
        var asset = await UploadTestImageAsync(
            "patch-visibility.png",
            "image/png",
            new byte[96]);

        asset.IsPublic.Should().BeTrue();

        var response = await PatchAsync(
            asset.Id,
            """
            {
              "isPublic": false
            }
            """);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await GetResponseDataAsync<AssetResponse>(response);
        patched.Should().NotBeNull();
        patched!.IsPublic.Should().BeFalse();
        patched.PublicUrl.Should().BeNull();
    }

    [Fact]
    public async Task Patch_Metadata_Should_Return_404_For_Missing_Asset()
    {
        var response = await PatchAsync(
            Guid.NewGuid(),
            """
            {
              "description": "missing"
            }
            """);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("asset_not_found");
    }

    private async Task<AssetResponse> UploadTestImageAsync(
        string fileName,
        string contentType,
        byte[] content,
        string? description = null,
        string? altText = null)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        requestContent.Add(fileContent, "File", fileName);

        if (description is not null)
        {
            requestContent.Add(new StringContent(description), "Description");
        }

        if (altText is not null)
        {
            requestContent.Add(new StringContent(altText), "AltText");
        }

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        return asset!;
    }

    private async Task<HttpResponseMessage> PatchAsync(Guid assetId, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Client.PatchAsync($"/api/v1/assets/{assetId}", content);
    }
}

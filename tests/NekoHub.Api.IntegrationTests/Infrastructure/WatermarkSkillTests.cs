using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Endpoints;
using NekoHub.Api.IntegrationTests.Setup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class WatermarkSkillTests : IntegrationTestBase
{
    public WatermarkSkillTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RunSkill_Should_Overwrite_Image_With_Watermark_And_Update_Metadata()
    {
        var assetId = await UploadTestPngAsync("watermark-source.png", CreatePngBytes(320, 180));
        var beforeAsset = await GetAssetAsync(assetId);
        beforeAsset.Should().NotBeNull();

        var storageKey = beforeAsset!.StorageKey;
        storageKey.Should().NotBeNullOrWhiteSpace();
        var beforeBytes = await ReadStoredBytesAsync(storageKey!);
        var beforeChecksum = beforeAsset.ChecksumSha256;

        var response = await Client.PostAsync(
            $"/api/v1/assets/{assetId}/skills/watermark/run",
            JsonContent.Create(new
            {
                parameters = new
                {
                    Text = "NekoHub Test",
                    Opacity = 0.7,
                    Position = "Center",
                    FontSize = 28
                }
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<RunAssetSkillResponse>(response);
        payload.Should().NotBeNull();
        payload!.Succeeded.Should().BeTrue(string.Join(
            "; ",
            payload.Steps.Select(step => $"{step.Name}:{step.ErrorMessage ?? "ok"}")));
        payload.SkillName.Should().Be("watermark");

        var afterAsset = await EventuallyAsync(
            () => GetAssetAsync(assetId),
            item => item is not null
                    && item.LatestExecutionSummary?.SkillName == "watermark"
                    && item.ChecksumSha256 != beforeChecksum);

        afterAsset.Should().NotBeNull();
        afterAsset!.StorageKey.Should().Be(storageKey);
        afterAsset.ContentType.Should().Be("image/png");
        afterAsset.Extension.Should().Be(".png");

        var afterBytes = await ReadStoredBytesAsync(storageKey!);
        afterBytes.Should().NotEqual(beforeBytes);
        ComputeSha256(afterBytes).Should().Be(afterAsset.ChecksumSha256);
        afterBytes.LongLength.Should().Be(afterAsset.Size);
    }

    private async Task<Guid> UploadTestPngAsync(string fileName, byte[] bytes)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);
        requestContent.Add(new StringContent("false"), "RunEnrichment");

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        return asset!.Id;
    }

    private async Task<AssetResponse?> GetAssetAsync(Guid assetId)
    {
        var response = await Client.GetAsync($"/api/v1/assets/{assetId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await GetResponseDataAsync<AssetResponse>(response);
    }

    private async Task<byte[]> ReadStoredBytesAsync(string storageKey)
    {
        await using var stream = File.OpenRead(ResolvePhysicalPath(storageKey));
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private string ResolvePhysicalPath(string storageKey)
    {
        return Path.Combine(Factory.TestStoragePath, storageKey.Replace('\\', '/').TrimStart('/'));
    }

    private static string ComputeSha256(byte[] content)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(content)).ToLowerInvariant();
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(20, 70, 140, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

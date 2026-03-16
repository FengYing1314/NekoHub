using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Endpoints;
using NekoHub.Api.IntegrationTests.Setup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class FormatConvertSkillTests : IntegrationTestBase
{
    public FormatConvertSkillTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RunSkill_Should_Convert_Image_And_Delete_Old_File_By_Default()
    {
        var assetId = await UploadTestPngAsync("format-convert-default.png", CreatePngBytes(4, 4));
        var beforeAsset = await GetAssetAsync(assetId);
        beforeAsset.Should().NotBeNull();

        var oldStorageKey = beforeAsset!.StorageKey;
        oldStorageKey.Should().NotBeNullOrWhiteSpace();
        File.Exists(ResolvePhysicalPath(oldStorageKey!)).Should().BeTrue();

        var response = await Client.PostAsync(
            $"/api/v1/assets/{assetId}/skills/format-convert/run",
            JsonContent.Create(new
            {
                parameters = new
                {
                    TargetFormat = "webp"
                }
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<RunAssetSkillResponse>(response);
        payload.Should().NotBeNull();
        payload!.Succeeded.Should().BeTrue(string.Join(
            "; ",
            payload.Steps.Select(step => $"{step.Name}:{step.ErrorMessage ?? "ok"}")));
        payload.SkillName.Should().Be("format-convert");

        var afterAsset = await EventuallyAsync(
            () => GetAssetAsync(assetId),
            item => item is not null
                    && item.StorageKey != oldStorageKey
                    && item.ContentType == "image/webp"
                    && item.Extension == ".webp");

        afterAsset.Should().NotBeNull();
        afterAsset!.LatestExecutionSummary?.SkillName.Should().Be("format-convert");
        afterAsset.StorageKey.Should().NotBe(oldStorageKey);
        afterAsset.ContentType.Should().Be("image/webp");
        afterAsset.Extension.Should().Be(".webp");

        var newBytes = await ReadStoredBytesAsync(afterAsset.StorageKey!);
        DetectFormatName(newBytes).Should().Be("WEBP");
        ComputeSha256(newBytes).Should().Be(afterAsset.ChecksumSha256);
        newBytes.LongLength.Should().Be(afterAsset.Size);

        File.Exists(ResolvePhysicalPath(oldStorageKey!)).Should().BeFalse();
        File.Exists(ResolvePhysicalPath(afterAsset.StorageKey!)).Should().BeTrue();
    }

    [Fact]
    public async Task RunSkill_With_KeepOriginal_True_Should_Preserve_Old_File()
    {
        var assetId = await UploadTestPngAsync("format-convert-keep.png", CreatePngBytes(5, 5));
        var beforeAsset = await GetAssetAsync(assetId);
        beforeAsset.Should().NotBeNull();

        var oldStorageKey = beforeAsset!.StorageKey;
        oldStorageKey.Should().NotBeNullOrWhiteSpace();
        File.Exists(ResolvePhysicalPath(oldStorageKey!)).Should().BeTrue();

        var response = await Client.PostAsync(
            $"/api/v1/assets/{assetId}/skills/format-convert/run",
            JsonContent.Create(new
            {
                parameters = new
                {
                    TargetFormat = "jpeg",
                    KeepOriginal = true
                }
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<RunAssetSkillResponse>(response);
        payload.Should().NotBeNull();
        payload!.Succeeded.Should().BeTrue(string.Join(
            "; ",
            payload.Steps.Select(step => $"{step.Name}:{step.ErrorMessage ?? "ok"}")));

        var afterAsset = await EventuallyAsync(
            () => GetAssetAsync(assetId),
            item => item is not null
                    && item.StorageKey != oldStorageKey
                    && item.ContentType == "image/jpeg"
                    && item.Extension == ".jpg");

        afterAsset.Should().NotBeNull();
        afterAsset!.StorageKey.Should().NotBe(oldStorageKey);
        afterAsset.ContentType.Should().Be("image/jpeg");
        afterAsset.Extension.Should().Be(".jpg");

        var newBytes = await ReadStoredBytesAsync(afterAsset.StorageKey!);
        DetectFormatName(newBytes).Should().Be("JPEG");
        ComputeSha256(newBytes).Should().Be(afterAsset.ChecksumSha256);
        newBytes.LongLength.Should().Be(afterAsset.Size);

        File.Exists(ResolvePhysicalPath(oldStorageKey!)).Should().BeTrue();
        File.Exists(ResolvePhysicalPath(afterAsset.StorageKey!)).Should().BeTrue();
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

    private static string DetectFormatName(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        var format = Image.DetectFormat(stream);
        format.Should().NotBeNull();
        return format!.Name.ToUpperInvariant();
    }

    private static string ComputeSha256(byte[] content)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(content)).ToLowerInvariant();
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(50, 200, 160, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

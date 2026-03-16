using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Endpoints;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Infrastructure.Persistence;
using NekoHub.Infrastructure.Storage.Local;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class ExifStripSkillTests : IntegrationTestBase
{
    public ExifStripSkillTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RunSkill_Should_Remove_Exif_And_Update_Asset_Metadata()
    {
        var originalBytes = CreateJpegWithExifBytes();
        var assetId = await UploadTestJpegAsync("exif-strip-source.jpg", originalBytes);

        var beforeAsset = await GetAssetAsync(assetId);
        beforeAsset.Should().NotBeNull();

        var beforeContent = await ReadStoredBytesAsync(assetId);
        HasExif(beforeContent).Should().BeTrue();
        ComputeSha256(beforeContent).Should().Be(beforeAsset!.ChecksumSha256);
        beforeContent.LongLength.Should().Be(beforeAsset.Size);

        var response = await Client.PostAsync(
            $"/api/v1/assets/{assetId}/skills/exif-strip/run",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<RunAssetSkillResponse>(response);
        payload.Should().NotBeNull();
        payload!.Succeeded.Should().BeTrue(string.Join(
            "; ",
            payload.Steps.Select(step => $"{step.Name}:{step.ErrorMessage ?? "ok"}")));
        payload.SkillName.Should().Be("exif-strip");

        var afterAsset = await EventuallyAsync(
            () => GetAssetAsync(assetId),
            item => item is not null
                    && item.LatestExecutionSummary?.SkillName == "exif-strip"
                    && item.ChecksumSha256 != beforeAsset.ChecksumSha256);

        var afterContent = await ReadStoredBytesAsync(assetId);
        HasExif(afterContent).Should().BeFalse();
        ComputeSha256(afterContent).Should().Be(afterAsset!.ChecksumSha256);
        afterContent.LongLength.Should().Be(afterAsset.Size);
        afterAsset.ChecksumSha256.Should().NotBe(beforeAsset.ChecksumSha256);
        afterAsset.Size.Should().BeLessThan(beforeAsset.Size);
    }

    private async Task<Guid> UploadTestJpegAsync(string fileName, byte[] bytes)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
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

    private async Task<byte[]> ReadStoredBytesAsync(Guid assetId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<LocalAssetStorage>();

        var asset = await dbContext.Assets
            .AsNoTracking()
            .SingleAsync(item => item.Id == assetId);

        await using var stream = await storage.OpenReadAsync(asset.StorageKey);
        stream.Should().NotBeNull();

        await using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static bool HasExif(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var image = Image.Load(stream);
        return image.Metadata.ExifProfile is not null;
    }

    private static string ComputeSha256(byte[] content)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(content)).ToLowerInvariant();
    }

    private static byte[] CreateJpegWithExifBytes()
    {
        using var image = new Image<Rgba32>(3, 3, new Rgba32(120, 40, 220, 255));
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Software, "NekoHub Integration Tests");
        exif.SetValue(ExifTag.Artist, "Codex");
        image.Metadata.ExifProfile = exif;

        using var output = new MemoryStream();
        image.Save(output, new JpegEncoder
        {
            Quality = 90
        });

        return output.ToArray();
    }
}

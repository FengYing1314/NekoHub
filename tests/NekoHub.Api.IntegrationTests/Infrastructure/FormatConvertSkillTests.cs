using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Endpoints;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Assets;
using NekoHub.Infrastructure.Persistence;
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
    public async Task Failed_Conversion_Should_Keep_Original_Asset_And_Save_Failure_Record_Independently()
    {
        using var factory = new FailingConversionFactory();
        using var client = factory.CreateClient();
        var originalBytes = CreatePngBytes(4, 4);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(originalBytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        form.Add(file, "File", "failed-conversion.png");
        form.Add(new StringContent("false"), "RunEnrichment");
        using var uploadResponse = await client.PostAsync("/api/v1/assets", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var original = (await GetResponseDataAsync<AssetResponse>(uploadResponse))!;
        var originalFiles = Directory.GetFiles(factory.TestStoragePath, "*", SearchOption.AllDirectories);
        factory.Failure.FailNextConversion = true;

        using var conversionResponse = await client.PostAsJsonAsync(
            $"/api/v1/assets/{original.Id}/skills/format-convert/run",
            new { parameters = new { TargetFormat = "jpeg", KeepOriginal = true } });
        conversionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var run = (await GetResponseDataAsync<RunAssetSkillResponse>(conversionResponse))!;
        run.Succeeded.Should().BeFalse();

        using var detailsResponse = await client.GetAsync($"/api/v1/assets/{original.Id}");
        var after = (await GetResponseDataAsync<AssetResponse>(detailsResponse))!;
        after.StorageKey.Should().Be(original.StorageKey);
        after.ContentType.Should().Be("image/png");
        after.ChecksumSha256.Should().Be(original.ChecksumSha256);
        after.Derivatives.Should().BeEmpty();
        after.LatestExecutionSummary.Should().NotBeNull();
        after.LatestExecutionSummary!.Succeeded.Should().BeFalse();
        Directory.GetFiles(factory.TestStoragePath, "*", SearchOption.AllDirectories).Should().BeEquivalentTo(originalFiles);
        (await File.ReadAllBytesAsync(Path.Combine(factory.TestStoragePath, original.StorageKey))).Should().Equal(originalBytes);
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
        afterAsset.Derivatives.Should().ContainSingle(derivative => derivative.Kind.StartsWith("original_"));

        using var originalResponse = await Client.GetAsync($"/content/{oldStorageKey}");
        originalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        DetectFormatName(await originalResponse.Content.ReadAsByteArrayAsync()).Should().Be("PNG");

        using var deleteResponse = await Client.DeleteAsync($"/api/v1/assets/{assetId}");
        deleteResponse.IsSuccessStatusCode.Should().BeTrue();
        File.Exists(ResolvePhysicalPath(oldStorageKey!)).Should().BeFalse();
        File.Exists(ResolvePhysicalPath(afterAsset.StorageKey!)).Should().BeFalse();
    }

    [Fact]
    public async Task Kept_Private_Original_Should_Require_Authenticated_Derivative_Download()
    {
        var originalBytes = CreatePngBytes(5, 5);
        var assetId = await UploadTestPngAsync("private-kept-original.png", originalBytes, isPublic: false);
        var before = (await GetAssetAsync(assetId))!;
        using var conversionResponse = await Client.PostAsJsonAsync(
            $"/api/v1/assets/{assetId}/skills/format-convert/run",
            new { parameters = new { TargetFormat = "jpeg", KeepOriginal = true } });
        conversionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var run = (await GetResponseDataAsync<RunAssetSkillResponse>(conversionResponse))!;
        run.Succeeded.Should().BeTrue(string.Join("; ", run.Steps.Select(step => step.ErrorMessage)));

        var after = (await GetAssetAsync(assetId))!;
        after.IsPublic.Should().BeFalse();
        var original = after.Derivatives.Single(derivative => derivative.Kind.StartsWith("original_", StringComparison.Ordinal));
        var protectedPath = $"/api/v1/assets/{assetId}/derivatives/{original.Kind}/content";

        using var protectedResponse = await Client.GetAsync(protectedPath);
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        protectedResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await protectedResponse.Content.ReadAsByteArrayAsync()).Should().Equal(originalBytes);

        using var anonymousClient = CreateAnonymousClient();
        using var anonymousResponse = await anonymousClient.GetAsync(protectedPath);
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var publicResponse = await anonymousClient.GetAsync($"/content/{before.StorageKey}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> UploadTestPngAsync(string fileName, byte[] bytes, bool isPublic = true)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);
        requestContent.Add(new StringContent("false"), "RunEnrichment");
        requestContent.Add(new StringContent(isPublic ? "true" : "false"), "IsPublic");

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        return asset!.Id;
    }

    private sealed class FailingConversionFactory : NekoHubApplicationFactory
    {
        public ConversionFailureInterceptor Failure { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddDbContext<AssetDbContext>(options => options.AddInterceptors(Failure)));
        }
    }

    private sealed class ConversionFailureInterceptor : SaveChangesInterceptor
    {
        public bool FailNextConversion { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailNextConversion && eventData.Context!.ChangeTracker.Entries<Asset>()
                    .Any(entry => entry.State == EntityState.Modified && entry.Entity.Extension == ".jpg"))
            {
                FailNextConversion = false;
                throw new InvalidOperationException("Simulated one-time conversion persistence failure.");
            }

            return ValueTask.FromResult(result);
        }
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

using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Infrastructure.Persistence;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AssetListQueryTests : IntegrationTestBase
{
    public AssetListQueryTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task List_With_Query_Should_Filter_By_FileName_Description_And_AltText()
    {
        var token = $"kw-{Guid.NewGuid():N}";

        var byFileName = await UploadTestImageAsync($"{token}-file.png", "image/png", new byte[128]);
        var byDescription = await UploadTestImageAsync(
            $"no-keyword-{Guid.NewGuid():N}.png",
            "image/png",
            new byte[96],
            description: $"desc-{token}");
        var byAltText = await UploadTestImageAsync(
            $"alt-only-{Guid.NewGuid():N}.png",
            "image/png",
            new byte[84],
            altText: $"alt-{token}");
        await UploadTestImageAsync($"other-{Guid.NewGuid():N}.png", "image/png", new byte[64]);

        var paged = await GetAssetListAsync($"/api/v1/assets?page=1&pageSize=50&query={token}");

        paged.Items.Should().Contain(item => item.Id == byFileName.Id);
        paged.Items.Should().Contain(item => item.Id == byDescription.Id);
        paged.Items.Should().Contain(item => item.Id == byAltText.Id);
        paged.Items.Should().OnlyContain(item =>
            (item.OriginalFileName != null && item.OriginalFileName.Contains(token, StringComparison.OrdinalIgnoreCase))
            || item.Id == byDescription.Id
            || item.Id == byAltText.Id);
    }

    [Fact]
    public async Task List_With_ContentType_Should_Filter_Exact_And_Wildcard()
    {
        var token = $"ct-{Guid.NewGuid():N}";
        var pngAsset = await UploadTestImageAsync($"{token}-png.png", "image/png", new byte[120]);
        var jpegAsset = await UploadTestImageAsync($"{token}-jpeg.jpg", "image/jpeg", new byte[160]);

        var exactPaged = await GetAssetListAsync($"/api/v1/assets?page=1&pageSize=50&query={token}&contentType=image/png");
        exactPaged.Items.Should().Contain(item => item.Id == pngAsset.Id);
        exactPaged.Items.Should().NotContain(item => item.Id == jpegAsset.Id);
        exactPaged.Items.Should().OnlyContain(item => item.ContentType == "image/png");

        var wildcardPaged = await GetAssetListAsync($"/api/v1/assets?page=1&pageSize=50&query={token}&contentType=image/*");
        wildcardPaged.Items.Should().Contain(item => item.Id == pngAsset.Id);
        wildcardPaged.Items.Should().Contain(item => item.Id == jpegAsset.Id);
    }

    [Fact]
    public async Task List_With_Size_Sort_Should_Support_Asc_And_Desc()
    {
        var token = $"size-{Guid.NewGuid():N}";
        await UploadTestImageAsync($"{token}-small.png", "image/png", new byte[80]);
        await UploadTestImageAsync($"{token}-middle.png", "image/png", new byte[160]);
        await UploadTestImageAsync($"{token}-large.png", "image/png", new byte[320]);

        var descPaged = await GetAssetListAsync($"/api/v1/assets?page=1&pageSize=50&query={token}&orderBy=size&orderDirection=desc");
        var descSizes = descPaged.Items.Select(item => item.Size).ToList();
        descSizes.Should().BeInDescendingOrder();

        var ascPaged = await GetAssetListAsync($"/api/v1/assets?page=1&pageSize=50&query={token}&orderBy=size&orderDirection=asc");
        var ascSizes = ascPaged.Items.Select(item => item.Size).ToList();
        ascSizes.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task List_With_CreatedAt_Sort_Should_Support_Asc_And_Desc()
    {
        var token = $"time-{Guid.NewGuid():N}";
        var first = await UploadTestImageAsync($"{token}-first.png", "image/png", new byte[88]);
        await Task.Delay(20);
        var second = await UploadTestImageAsync($"{token}-second.png", "image/png", new byte[96]);

        var ascPaged = await GetAssetListAsync($"/api/v1/assets?page=1&pageSize=50&query={token}&orderBy=createdAtUtc&orderDirection=asc");
        ascPaged.Items.Should().HaveCount(2);
        ascPaged.Items[0].Id.Should().Be(first.Id);
        ascPaged.Items[1].Id.Should().Be(second.Id);

        var descPaged = await GetAssetListAsync($"/api/v1/assets?page=1&pageSize=50&query={token}&orderBy=createdAtUtc&orderDirection=desc");
        descPaged.Items.Should().HaveCount(2);
        descPaged.Items[0].Id.Should().Be(second.Id);
        descPaged.Items[1].Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task List_With_Status_Should_Filter_By_AssetStatus()
    {
        var token = $"status-{Guid.NewGuid():N}";
        var readyAsset = await UploadTestImageAsync($"{token}-ready.png", "image/png", new byte[80]);
        var failedAsset = await UploadTestImageAsync($"{token}-failed.png", "image/png", new byte[96]);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
            var asset = await dbContext.Assets.SingleAsync(x => x.Id == failedAsset.Id);
            asset.MarkFailed();
            await dbContext.SaveChangesAsync();
        }

        var paged = await GetAssetListAsync($"/api/v1/assets?page=1&pageSize=50&query={token}&status=failed");

        paged.Items.Should().ContainSingle(item => item.Id == failedAsset.Id);
        paged.Items.Should().NotContain(item => item.Id == readyAsset.Id);
    }

    [Fact]
    public async Task List_With_Invalid_Query_Should_Fallback_Instead_Of_500()
    {
        var response = await Client.GetAsync("/api/v1/assets?page=0&pageSize=1000&orderBy=invalid&orderDirection=invalid&status=invalid");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await GetResponseDataAsync<AssetPagedResponse>(response);
        paged.Should().NotBeNull();
        paged!.Page.Should().Be(1);
        paged.PageSize.Should().Be(100);
        paged.Total.Should().BeGreaterThanOrEqualTo(0);
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

        if (!string.IsNullOrWhiteSpace(description))
        {
            requestContent.Add(new StringContent(description), "Description");
        }

        if (!string.IsNullOrWhiteSpace(altText))
        {
            requestContent.Add(new StringContent(altText), "AltText");
        }

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        return asset!;
    }

    private async Task<AssetPagedResponse> GetAssetListAsync(string pathAndQuery)
    {
        var response = await Client.GetAsync(pathAndQuery);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var paged = await GetResponseDataAsync<AssetPagedResponse>(response);
        paged.Should().NotBeNull();
        return paged!;
    }
}

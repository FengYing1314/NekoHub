using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Endpoints;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class AssetEnrichmentToggleTests : IntegrationTestBase
{
    public AssetEnrichmentToggleTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Upload_With_RunEnrichment_False_Should_Skip_Skill_Dispatch()
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePngBytes(2, 2));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", "run-enrichment-disabled.png");
        requestContent.Add(new StringContent("false"), "RunEnrichment");

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        var uploadedAsset = asset!;

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();

        var executions = await dbContext.SkillExecutions
            .AsNoTracking()
            .Where(record => record.SourceAssetId == uploadedAsset.Id)
            .ToListAsync();
        executions.Should().BeEmpty();

        var derivatives = await dbContext.AssetDerivatives
            .AsNoTracking()
            .Where(item => item.SourceAssetId == uploadedAsset.Id)
            .ToListAsync();
        derivatives.Should().BeEmpty();

        var structuredResults = await dbContext.AssetStructuredResults
            .AsNoTracking()
            .Where(item => item.SourceAssetId == uploadedAsset.Id)
            .ToListAsync();
        structuredResults.Should().BeEmpty();

        var detailResponse = await Client.GetAsync($"/api/v1/assets/{uploadedAsset.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.LatestExecutionSummary.Should().BeNull();
        detail.Derivatives.Should().BeEmpty();
        detail.StructuredResults.Should().BeEmpty();
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(80, 140, 255, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

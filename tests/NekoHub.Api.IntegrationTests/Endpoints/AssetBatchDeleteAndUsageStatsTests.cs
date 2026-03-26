using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AssetBatchDeleteAndUsageStatsTests : IntegrationTestBase
{
    private const string ProtocolVersion = "2025-11-25";

    public AssetBatchDeleteAndUsageStatsTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Batch_Delete_Should_Reuse_Single_Delete_Semantics_And_Report_NotFoundIds()
    {
        var asset1 = await UploadTestPngAsync("batch-delete-1.png");
        var asset2 = await UploadTestPngAsync("batch-delete-2.png");
        var missingId = Guid.NewGuid();

        var response = await Client.PostAsJsonAsync(
            "/api/v1/assets/batch-delete",
            new[] { asset1.Id, asset2.Id, missingId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await GetResponseDataAsync<BatchDeleteAssetsResponse>(response);
        result.Should().NotBeNull();
        result!.RequestedCount.Should().Be(3);
        result.DeletedCount.Should().Be(2);
        result.NotFoundIds.Should().ContainSingle(id => id == missingId);

        (await Client.GetAsync($"/api/v1/assets/{asset1.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/v1/assets/{asset2.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Usage_Stats_Should_Only_Count_Active_Assets_And_Return_Most_Active_Skill()
    {
        var activeAsset = await UploadTestPngAsync("usage-active.png");
        var deletedAsset = await UploadTestPngAsync("usage-deleted.png");

        var deleteResponse = await Client.DeleteAsync($"/api/v1/assets/{deletedAsset.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var runSkillResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 901,
            method = "tools/call",
            @params = new
            {
                name = "run_asset_skill",
                arguments = new
                {
                    assetId = activeAsset.Id,
                    skillName = "basic_image_enrich"
                }
            }
        });

        runSkillResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var runSkillJson = await ReadJsonObjectAsync(runSkillResponse);
        runSkillJson["result"]?["isError"].Should().BeNull();

        var response = await Client.GetAsync("/api/v1/assets/usage-stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await GetResponseDataAsync<AssetUsageStatsResponse>(response);
        stats.Should().NotBeNull();
        stats!.TotalAssets.Should().Be(1);
        stats.TotalBytes.Should().Be(activeAsset.Size);
        stats.TotalDerivatives.Should().Be(1);
        stats.ContentTypeBreakdown.Should().ContainSingle();
        stats.ContentTypeBreakdown[0].ContentType.Should().Be("image/png");
        stats.ContentTypeBreakdown[0].Count.Should().Be(1);
        stats.ContentTypeBreakdown[0].TotalBytes.Should().Be(activeAsset.Size);
        stats.MostActiveSkill.Should().NotBeNull();
        stats.MostActiveSkill!.SkillName.Should().Be("basic_image_enrich");
        stats.MostActiveSkill.RunCount.Should().Be(2);
    }

    private async Task<AssetResponse> UploadTestPngAsync(string fileName)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePngBytes(1, 1));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        return asset!;
    }

    private async Task<HttpResponseMessage> PostMcpAsync(object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", ProtocolVersion);

        return await Client.SendAsync(request);
    }

    private static async Task<JsonObject> ReadJsonObjectAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(raw);
        node.Should().NotBeNull();
        node.Should().BeOfType<JsonObject>();
        return (JsonObject)node!;
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(40, 200, 120, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

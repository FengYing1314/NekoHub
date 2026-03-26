using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
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

public class SkillExecutionRecordsTests : IntegrationTestBase
{
    private const string ProtocolVersion = "2025-11-25";

    public SkillExecutionRecordsTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Upload_Processing_Should_Persist_Skill_Execution_Record()
    {
        var assetId = await UploadTestPngAsync("skill-record-upload.png");

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();

        var execution = await dbContext.SkillExecutions
            .AsNoTracking()
            .Where(record => record.SourceAssetId == assetId)
            .OrderByDescending(record => record.StartedAtUtc)
            .FirstOrDefaultAsync();

        execution.Should().NotBeNull();
        execution!.SkillName.Should().Be("basic_image_enrich");
        execution.TriggerSource.Should().Be("upload");
        execution.Succeeded.Should().BeTrue();
        execution.ParametersJson.Should().BeNull();
        execution.CompletedAtUtc.Should().BeOnOrAfter(execution.StartedAtUtc);

        var stepResults = await dbContext.SkillExecutionStepResults
            .AsNoTracking()
            .Where(step => step.SkillExecutionId == execution.Id)
            .OrderBy(step => step.StartedAtUtc)
            .ToListAsync();

        stepResults.Should().HaveCount(2);
        stepResults.Select(step => step.StepName)
            .Should()
            .Contain(["generate_thumbnail", "generate_basic_caption"]);
        stepResults.All(step => step.CompletedAtUtc >= step.StartedAtUtc).Should().BeTrue();

        var detailResponse = await Client.GetAsync($"/api/v1/assets/{assetId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.LatestExecutionSummary.Should().NotBeNull();
        detail.LatestExecutionSummary!.TriggerSource.Should().Be("upload");
        detail.LatestExecutionSummary.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Manual_Skill_Run_Should_Persist_Manual_Execution_Record_And_Update_Latest_Summary()
    {
        var assetId = await UploadTestPngAsync("skill-record-manual.png");

        var runResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 701,
            method = "tools/call",
            @params = new
            {
                name = "run_asset_skill",
                arguments = new
                {
                    assetId,
                    skillName = "basic_image_enrich",
                    parameters = new
                    {
                        quality = "high",
                        force = true,
                        maxWidth = 256
                    }
                }
            }
        }, ProtocolVersion);

        runResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var runJson = await ReadJsonObjectAsync(runResponse);
        runJson["result"]?["isError"].Should().BeNull();
        runJson["result"]?["structuredContent"]?["succeeded"]?.GetValue<bool>().Should().BeTrue();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        var manualExecution = await dbContext.SkillExecutions
            .AsNoTracking()
            .Where(record => record.SourceAssetId == assetId && record.TriggerSource == "manual")
            .OrderByDescending(record => record.StartedAtUtc)
            .FirstOrDefaultAsync();

        manualExecution.Should().NotBeNull();
        manualExecution!.SkillName.Should().Be("basic_image_enrich");
        manualExecution.ParametersJson.Should().NotBeNullOrWhiteSpace();
        manualExecution.CompletedAtUtc.Should().BeOnOrAfter(manualExecution.StartedAtUtc);

        var parameters = JsonNode.Parse(manualExecution.ParametersJson!)?.AsObject();
        parameters.Should().NotBeNull();
        parameters!["quality"]?.GetValue<string>().Should().Be("high");
        parameters["force"]?.GetValue<bool>().Should().BeTrue();
        parameters["maxWidth"]?.GetValue<int>().Should().Be(256);

        var manualSteps = await dbContext.SkillExecutionStepResults
            .AsNoTracking()
            .Where(step => step.SkillExecutionId == manualExecution.Id)
            .ToListAsync();
        manualSteps.Should().NotBeEmpty();

        var detailResponse = await Client.GetAsync($"/api/v1/assets/{assetId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.LatestExecutionSummary.Should().NotBeNull();
        detail.LatestExecutionSummary!.ExecutionId.Should().Be(manualExecution.Id);
        detail.LatestExecutionSummary.TriggerSource.Should().Be("manual");
    }

    private async Task<Guid> UploadTestPngAsync(string fileName)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePngBytes(1, 1));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        return asset!.Id;
    }

    private async Task<HttpResponseMessage> PostMcpAsync(object payload, string? protocolVersion = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        if (!string.IsNullOrWhiteSpace(protocolVersion))
        {
            request.Headers.Add("MCP-Protocol-Version", protocolVersion);
        }

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
        using var image = new Image<Rgba32>(width, height, new Rgba32(80, 140, 255, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

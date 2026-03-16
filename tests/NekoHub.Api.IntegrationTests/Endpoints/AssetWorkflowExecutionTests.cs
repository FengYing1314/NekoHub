using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Application.Abstractions.Skills;
using NekoHub.Domain.Skills;
using NekoHub.Domain.Workflows;
using NekoHub.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AssetWorkflowExecutionTests : IntegrationTestBase
{
    public AssetWorkflowExecutionTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Upload_With_AutoRun_Workflow_Should_Run_Configured_Skill_Sequence()
    {
        await ResetWorkflowsAsync();
        await CreateWorkflowAsync(
            name: "auto-run-image-pipeline",
            isAutoRun: true,
            graphJson:
            """
            {
              "nodes": [
                { "id": "node-1", "type": "thumbnail" },
                { "id": "node-2", "data": { "skillId": "ai-caption" } }
              ]
            }
            """);

        var assetId = await UploadTestPngAsync("workflow-auto-run.png");

        var executions = await EventuallyAsync(
            () => ListExecutionsAsync(assetId, SkillTriggerSources.Upload),
            items => items.Count >= 2);

        executions.Select(execution => execution.SkillName)
            .Should()
            .Equal("thumbnail", "ai-caption");

        var latestDetail = await EventuallyAsync(async () =>
        {
            var response = await Client.GetAsync($"/api/v1/assets/{assetId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await GetResponseDataAsync<AssetResponse>(response);
        }, item => item?.LatestExecutionSummary?.SkillName == "ai-caption");

        latestDetail.Should().NotBeNull();
        latestDetail!.LatestExecutionSummary.Should().NotBeNull();
        latestDetail.LatestExecutionSummary!.TriggerSource.Should().Be(SkillTriggerSources.Upload);
        latestDetail.LatestExecutionSummary.SkillName.Should().Be("ai-caption");
    }

    [Fact]
    public async Task RunWorkflow_Should_Queue_Manual_Workflow_Execution_And_Return_Accepted()
    {
        await ResetWorkflowsAsync();
        var workflowId = await CreateWorkflowAsync(
            name: "manual-image-pipeline",
            isAutoRun: false,
            graphJson:
            """
            {
              "nodes": [
                { "id": "node-1", "data": { "skillId": "thumbnail" } },
                { "id": "node-2", "data": { "skillId": "ai-caption" } }
              ]
            }
            """);

        var assetId = await UploadTestPngAsync("workflow-manual-run.png", runEnrichment: false);

        var response = await Client.PostAsync($"/api/v1/assets/{assetId}/workflows/{workflowId}/run", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var payload = await GetResponseDataAsync<RunAssetWorkflowResponse>(response);
        payload.Should().NotBeNull();
        payload!.AssetId.Should().Be(assetId);
        payload.WorkflowId.Should().Be(workflowId);
        payload.SkillIds.Should().Equal("thumbnail", "ai-caption");

        var executions = await EventuallyAsync(
            () => ListExecutionsAsync(assetId, SkillTriggerSources.Manual),
            items => items.Count >= 2);

        executions.Select(execution => execution.SkillName)
            .Should()
            .Equal("thumbnail", "ai-caption");
    }

    [Fact]
    public async Task RunWorkflow_Should_Persist_Node_Parameters_Per_Skill()
    {
        await ResetWorkflowsAsync();
        var workflowId = await CreateWorkflowAsync(
            name: "manual-parameterized-pipeline",
            isAutoRun: false,
            graphJson:
            """
            {
              "nodes": [
                {
                  "id": "node-1",
                  "data": {
                    "skillId": "thumbnail",
                    "parameters": {
                      "maxSize": 128,
                      "format": "png"
                    }
                  }
                },
                {
                  "id": "node-2",
                  "data": {
                    "skillId": "ai-caption",
                    "prompt": "short caption"
                  }
                }
              ]
            }
            """);

        var assetId = await UploadTestPngAsync("workflow-parameters.png", runEnrichment: false);

        var response = await Client.PostAsync($"/api/v1/assets/{assetId}/workflows/{workflowId}/run", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var executions = await EventuallyAsync(
            () => ListExecutionsAsync(assetId, SkillTriggerSources.Manual),
            items => items.Count >= 2);

        executions.Select(execution => execution.SkillName)
            .Should()
            .Equal("thumbnail", "ai-caption");

        var thumbnailParameters = JsonNode.Parse(executions[0].ParametersJson!)?.AsObject();
        thumbnailParameters.Should().NotBeNull();
        thumbnailParameters!["maxSize"]?.GetValue<int>().Should().Be(128);
        thumbnailParameters["format"]?.GetValue<string>().Should().Be("png");

        var captionParameters = JsonNode.Parse(executions[1].ParametersJson!)?.AsObject();
        captionParameters.Should().NotBeNull();
        captionParameters!["prompt"]?.GetValue<string>().Should().Be("short caption");
        captionParameters["skillId"].Should().BeNull();
    }

    private async Task ResetWorkflowsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        // 每个用例先清空 workflow，避免 auto-run 配置在同一个 factory 生命周期内串测试。
        var workflows = await dbContext.WorkflowProfiles.ToListAsync();
        dbContext.WorkflowProfiles.RemoveRange(workflows);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> CreateWorkflowAsync(string name, bool isAutoRun, string graphJson)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();

        var workflow = new WorkflowProfile(
            id: Guid.CreateVersion7(),
            name: name,
            description: "Integration test workflow",
            isAutoRun: isAutoRun,
            graphJson: graphJson);

        dbContext.WorkflowProfiles.Add(workflow);
        await dbContext.SaveChangesAsync();
        return workflow.Id;
    }

    private async Task<IReadOnlyList<SkillExecution>> ListExecutionsAsync(Guid assetId, string triggerSource)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        return await dbContext.SkillExecutions
            .AsNoTracking()
            .Where(execution => execution.SourceAssetId == assetId && execution.TriggerSource == triggerSource)
            // startedAtUtc 理论上已经足够，但同一时刻写入时再用 Id 兜底保持断言顺序稳定。
            .OrderBy(execution => execution.StartedAtUtc)
            .ThenBy(execution => execution.Id)
            .ToListAsync();
    }

    private async Task<Guid> UploadTestPngAsync(string fileName, bool runEnrichment = true)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePngBytes(2, 2));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);
        requestContent.Add(new StringContent(runEnrichment ? "true" : "false"), "RunEnrichment");

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        return asset!.Id;
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(80, 140, 255, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

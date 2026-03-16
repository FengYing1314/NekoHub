using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AssetCaptionBackfillTests : IntegrationTestBase, IClassFixture<NekoHubApiKeyApplicationFactory>
{
    private const string ProtocolVersion = "2025-11-25";

    public AssetCaptionBackfillTests(NekoHubApiKeyApplicationFactory factory) : base(factory)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);
    }

    [Fact]
    public async Task Upload_Without_Text_Metadata_Should_Backfill_Description_And_AltText_From_Basic_Caption()
    {
        var assetId = await UploadTestPngAsync("caption-backfill.png");

        var detail = await EventuallyAsync(async () =>
        {
            var response = await Client.GetAsync($"/api/v1/assets/{assetId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await GetResponseDataAsync<AssetResponse>(response);
        }, item => item is not null
            && item.Description == "stub caption"
            && item.AltText == "stub caption"
            && item.StructuredResults.Any(result => result.Kind == "basic_caption"));

        detail.Should().NotBeNull();
        detail!.Description.Should().Be("stub caption");
        detail.AltText.Should().Be("stub caption");
    }

    [Fact]
    public async Task Upload_With_Manual_Text_Metadata_Should_Not_Be_Overwritten_By_Basic_Caption()
    {
        var assetId = await UploadTestPngAsync(
            "caption-preserve.png",
            description: "manual description",
            altText: "manual alt text");

        var detail = await EventuallyAsync(async () =>
        {
            var response = await Client.GetAsync($"/api/v1/assets/{assetId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await GetResponseDataAsync<AssetResponse>(response);
        }, item => item is not null
            && item.StructuredResults.Any(result => result.Kind == "basic_caption"));

        detail.Should().NotBeNull();
        detail!.Description.Should().Be("manual description");
        detail.AltText.Should().Be("manual alt text");
    }

    [Fact]
    public async Task Manual_Rerun_Should_Backfill_From_Existing_Basic_Caption_Without_Creating_Duplicate_Result()
    {
        var assetId = await UploadTestPngAsync("caption-rerun.png");

        await EventuallyAsync(async () =>
        {
            var response = await Client.GetAsync($"/api/v1/assets/{assetId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await GetResponseDataAsync<AssetResponse>(response);
        }, item => item is not null
            && item.Description == "stub caption"
            && item.StructuredResults.Count(result => result.Kind == "basic_caption") == 1);

        var patchResponse = await Client.PatchAsJsonAsync($"/api/v1/assets/{assetId}", new
        {
            description = (string?)null,
            altText = (string?)null
        });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var runSkillResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 801,
            method = "tools/call",
            @params = new
            {
                name = "run_asset_skill",
                arguments = new
                {
                    assetId,
                    skillName = "basic_image_enrich"
                }
            }
        }, ProtocolVersion);
        runSkillResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await EventuallyAsync(async () =>
        {
            var response = await Client.GetAsync($"/api/v1/assets/{assetId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await GetResponseDataAsync<AssetResponse>(response);
        }, item => item is not null
            && item.Description == "stub caption"
            && item.AltText == "stub caption"
            && item.StructuredResults.Count(result => result.Kind == "basic_caption") == 1);

        detail.Should().NotBeNull();
        detail!.Description.Should().Be("stub caption");
        detail.AltText.Should().Be("stub caption");
        detail.StructuredResults.Count(result => result.Kind == "basic_caption").Should().Be(1);
    }

    [Fact]
    public async Task Upload_When_Standard_Ai_Response_Has_No_Content_Should_Fallback_To_Streaming_And_Backfill_Metadata()
    {
        using var factory = new CompatibleAiVisionApplicationFactory();
        factory.VisionHandler.EnqueueJson(new
        {
            model = "gpt-5.4",
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = (string?)null
                    }
                }
            }
        });
        factory.VisionHandler.EnqueueStreaming(
            """{"model":"gpt-5.4","choices":[{"delta":{"reasoning_content":"Inspecting image"}}]}""",
            """{"model":"gpt-5.4","choices":[{"delta":{"content":"{\"caption\":\""}}]}""",
            """{"model":"gpt-5.4","choices":[{"delta":{"content":"caption recovered from streaming fallback"}}]}""",
            """{"model":"gpt-5.4","choices":[{"delta":{"content":"\"}"}}]}"""
        );
        factory.EnsureTestingAiRuntime();

        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);

        var assetId = await UploadTestPngAsync(client, "caption-streaming-fallback.png", JsonOptions);

        var detail = await EventuallyAsync(async () =>
        {
            var response = await client.GetAsync($"/api/v1/assets/{assetId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await GetResponseDataAsync<AssetResponse>(response);
        }, item => item is not null
            && item.Description == "caption recovered from streaming fallback"
            && item.AltText == "caption recovered from streaming fallback"
            && item.StructuredResults.Any(result => result.Kind == "basic_caption"));

        detail.Should().NotBeNull();
        detail!.Description.Should().Be("caption recovered from streaming fallback");
        detail.AltText.Should().Be("caption recovered from streaming fallback");
        factory.VisionHandler.RequestBodies.Should().HaveCount(2);
    }

    private async Task<Guid> UploadTestPngAsync(
        string fileName,
        string? description = null,
        string? altText = null)
    {
        return await UploadTestPngAsync(Client, fileName, JsonOptions, description, altText);
    }

    private static async Task<Guid> UploadTestPngAsync(
        HttpClient client,
        string fileName,
        JsonSerializerOptions jsonSerializerOptions,
        string? description = null,
        string? altText = null)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePngBytes(2, 2));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);

        if (description is not null)
        {
            requestContent.Add(new StringContent(description), "Description");
        }

        if (altText is not null)
        {
            requestContent.Add(new StringContent(altText), "AltText");
        }

        var response = await client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<AssetResponse>>(jsonSerializerOptions);
        var asset = payload?.Data;
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

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(80, 140, 255, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NekoHub.Api.IntegrationTests.Setup;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class McpPromptSurfaceTests : IntegrationTestBase, IClassFixture<NekoHubApiKeyApplicationFactory>
{
    private const string ProtocolVersion = "2025-11-25";

    public McpPromptSurfaceTests(NekoHubApiKeyApplicationFactory factory) : base(factory)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);
    }

    [Fact]
    public async Task Mcp_Initialize_And_Prompts_List_Should_Expose_Prompt_Surface()
    {
        var initializeResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = ProtocolVersion,
                clientInfo = new
                {
                    name = "integration-test",
                    version = "1.0.0"
                },
                capabilities = new { }
            }
        });

        initializeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initializeJson = await ReadJsonObjectAsync(initializeResponse);
        initializeJson["result"]?["capabilities"]?["prompts"]?["listChanged"]?.GetValue<bool>().Should().BeFalse();

        var listResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "prompts/list"
        }, ProtocolVersion);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await ReadJsonObjectAsync(listResponse);
        var prompts = listJson["result"]?["prompts"]?.AsArray();
        prompts.Should().NotBeNull();

        var promptNames = prompts!
            .Select(static node => node?["name"]?.GetValue<string>())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList();

        promptNames.Should().Contain(["inspect_asset", "enrich_asset", "review_asset_outputs"]);

        var enrichPrompt = prompts!.Single(static node => node?["name"]?.GetValue<string>() == "enrich_asset");
        enrichPrompt?["arguments"]?.AsArray().Should().NotBeNull();
        enrichPrompt?["arguments"]?.AsArray()!
            .Select(node => node?["name"]?.GetValue<string>())
            .Should()
            .Contain(["assetId", "skillName"]);
    }

    [Fact]
    public async Task Mcp_Prompts_Get_Should_Return_Inspect_Asset_Template()
    {
        var assetId = Guid.NewGuid();
        var response = await PostPromptGetAsync(
            id: 11,
            name: "inspect_asset",
            arguments: new
            {
                assetId
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        json["result"]?["name"]?.GetValue<string>().Should().Be("inspect_asset");

        var messages = json["result"]?["messages"]?.AsArray();
        messages.Should().NotBeNull();
        messages.Should().NotBeEmpty();

        var content = messages![0]?["content"]?.AsObject();
        content.Should().NotBeNull();
        content!["type"]?.GetValue<string>().Should().Be("text");

        var text = content["text"]?.GetValue<string>();
        text.Should().NotBeNullOrWhiteSpace();
        text.Should().Contain("get_asset");
        text.Should().Contain($"asset://{assetId}");
        text.Should().Contain("structured-results");
    }

    [Fact]
    public async Task Mcp_Prompts_Get_Should_Default_Skill_Name_For_Enrich_Asset()
    {
        var assetId = Guid.NewGuid();
        var response = await PostPromptGetAsync(
            id: 12,
            name: "enrich_asset",
            arguments: new
            {
                assetId
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        var text = json["result"]?["messages"]?[0]?["content"]?["text"]?.GetValue<string>();
        text.Should().NotBeNullOrWhiteSpace();
        text.Should().Contain("run_asset_skill");
        text.Should().Contain("basic_image_enrich");
    }

    [Fact]
    public async Task Mcp_Prompts_Get_Should_Return_Prompt_Not_Found()
    {
        var response = await PostPromptGetAsync(
            id: 21,
            name: "unknown_prompt",
            arguments: new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        json["error"]?["code"]?.GetValue<int>().Should().Be(-32601);
        json["error"]?["data"]?["code"]?.GetValue<string>().Should().Be("prompt_not_found");
    }

    [Fact]
    public async Task Mcp_Prompts_Get_Should_Validate_Required_Arguments()
    {
        var response = await PostPromptGetAsync(
            id: 22,
            name: "inspect_asset",
            arguments: new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        json["error"]?["code"]?.GetValue<int>().Should().Be(-32602);
        json["error"]?["data"]?["code"]?.GetValue<string>().Should().Be("prompt_argument_invalid");
    }

    private async Task<HttpResponseMessage> PostPromptGetAsync(int id, string name, object arguments)
    {
        return await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id,
            method = "prompts/get",
            @params = new
            {
                name,
                arguments
            }
        }, ProtocolVersion);
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
}

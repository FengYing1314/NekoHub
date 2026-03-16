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

public class McpResourceSurfaceTests : IntegrationTestBase, IClassFixture<NekoHubApiKeyApplicationFactory>
{
    private const string ProtocolVersion = "2025-11-25";

    public McpResourceSurfaceTests(NekoHubApiKeyApplicationFactory factory) : base(factory)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);
    }

    [Fact]
    public async Task Mcp_Initialize_And_Resources_List_Should_Expose_Resource_Surface()
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
        initializeJson["result"]?["capabilities"]?["resources"]?["subscribe"]?.GetValue<bool>().Should().BeFalse();
        initializeJson["result"]?["capabilities"]?["resources"]?["listChanged"]?.GetValue<bool>().Should().BeFalse();

        var listResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "resources/list"
        }, ProtocolVersion);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await ReadJsonObjectAsync(listResponse);
        var resources = listJson["result"]?["resources"]?.AsArray();
        resources.Should().NotBeNull();

        var uris = resources!
            .Select(static node => node?["uri"]?.GetValue<string>())
            .Where(static uri => !string.IsNullOrWhiteSpace(uri))
            .Cast<string>()
            .ToList();

        uris.Should().Contain("asset://{id}");
        uris.Should().Contain("asset://{id}/derivatives");
        uris.Should().Contain("asset://{id}/structured-results");
        uris.Should().Contain("skill://{name}");
        uris.Should().Contain(static uri => uri.StartsWith("skill://", StringComparison.OrdinalIgnoreCase)
                                            && !string.Equals(uri, "skill://{name}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mcp_Asset_Resources_Should_Reuse_Read_Model_And_Hide_Storage_Internals()
    {
        var assetId = await UploadTestPngAsync("mcp-resource-asset.png");

        var detail = await WaitForResourceObjectAsync(
            11,
            $"asset://{assetId}",
            item => item["derivatives"]?.AsArray()?.Count > 0);
        detail["id"]?.GetValue<string>().Should().Be(assetId.ToString());
        detail["isPublic"]?.GetValue<bool>().Should().BeTrue();
        detail["storageProvider"].Should().BeNull();
        detail["storageKey"].Should().BeNull();
        detail["derivatives"]?.AsArray().Should().NotBeEmpty();
        detail["structuredResults"]?.AsArray().Should().NotBeNull();

        var derivatives = await WaitForResourceObjectAsync(
            12,
            $"asset://{assetId}/derivatives",
            item => item["derivatives"]?.AsArray()?.Count > 0);
        derivatives["assetId"]?.GetValue<string>().Should().Be(assetId.ToString());
        derivatives["derivatives"]?.AsArray().Should().NotBeEmpty();

        var structuredResults = await WaitForResourceObjectAsync(
            13,
            $"asset://{assetId}/structured-results",
            item => item["structuredResults"]?.AsArray() is not null);
        structuredResults["assetId"]?.GetValue<string>().Should().Be(assetId.ToString());
        structuredResults["structuredResults"]?.AsArray().Should().NotBeNull();
    }

    [Fact]
    public async Task Mcp_Skill_Resource_Should_Return_Skill_Metadata()
    {
        var response = await ReadResourceAsync(21, "skill://basic_image_enrich");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var skill = await ReadResourceContentAsObjectAsync(response);
        skill["skillName"]?.GetValue<string>().Should().Be("basic_image_enrich");
        skill["description"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        skill["steps"]?.AsArray()
            .Select(node => node?.GetValue<string>())
            .Should()
            .Contain(["generate_thumbnail", "generate_basic_caption"]);
    }

    [Fact]
    public async Task Mcp_Resource_Read_Should_Return_Skill_Not_Found_For_Unknown_Skill()
    {
        var response = await ReadResourceAsync(31, "skill://unknown_skill");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await ReadJsonObjectAsync(response);
        json["error"]?["code"]?.GetValue<int>().Should().Be(-32602);
        json["error"]?["data"]?["code"]?.GetValue<string>().Should().Be("skill_not_found");
    }

    private async Task<HttpResponseMessage> ReadResourceAsync(int id, string uri)
    {
        return await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id,
            method = "resources/read",
            @params = new
            {
                uri
            }
        }, ProtocolVersion);
    }

    private async Task<Guid> UploadTestPngAsync(string fileName)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePngBytes(1, 1));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);

        var response = await Client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

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

    private async Task<JsonObject> WaitForResourceObjectAsync(
        int id,
        string uri,
        Func<JsonObject, bool> predicate)
    {
        return await EventuallyAsync(async () =>
        {
            var response = await ReadResourceAsync(id, uri);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await ReadResourceContentAsObjectAsync(response);
        }, predicate);
    }

    private static async Task<JsonObject> ReadResourceContentAsObjectAsync(HttpResponseMessage response)
    {
        var json = await ReadJsonObjectAsync(response);
        var contents = json["result"]?["contents"]?.AsArray();
        contents.Should().NotBeNull();
        contents.Should().NotBeEmpty();
        contents![0]?["mimeType"]?.GetValue<string>().Should().Be("application/json");

        var text = contents[0]?["text"]?.GetValue<string>();
        text.Should().NotBeNullOrWhiteSpace();

        var contentNode = JsonNode.Parse(text!);
        contentNode.Should().NotBeNull();
        contentNode.Should().BeOfType<JsonObject>();
        return (JsonObject)contentNode!;
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

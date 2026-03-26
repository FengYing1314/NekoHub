using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class McpToolSurfaceTests : IntegrationTestBase
{
    private const string ProtocolVersion = "2025-11-25";

    public McpToolSurfaceTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Mcp_Initialize_And_Tools_List_Should_Expose_Asset_Tool_Surface()
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
        initializeResponse.Headers.GetValues("MCP-Protocol-Version").Single().Should().Be(ProtocolVersion);

        var initializeJson = await ReadJsonObjectAsync(initializeResponse);
        initializeJson["result"]?["protocolVersion"]?.GetValue<string>().Should().Be(ProtocolVersion);
        initializeJson["result"]?["capabilities"]?["tools"]?["listChanged"]?.GetValue<bool>().Should().BeFalse();
        initializeJson["result"]?["serverInfo"]?["name"]?.GetValue<string>().Should().Be("NekoHub MCP");

        var listResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list"
        }, ProtocolVersion);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await ReadJsonObjectAsync(listResponse);
        var tools = listJson["result"]?["tools"]?.AsArray();
        tools.Should().NotBeNull();

        var toolNames = tools!
            .Select(static node => node?["name"]?.GetValue<string>())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList();

        toolNames.Should().Contain([
            "get_asset",
            "list_assets",
            "patch_asset",
            "list_skills",
            "get_asset_content_url",
            "upload_asset",
            "run_asset_skill",
            "delete_asset",
            "batch_delete_assets",
            "get_asset_usage_stats"
        ]);

        var getAssetTool = tools!.Single(static node => node?["name"]?.GetValue<string>() == "get_asset");
        getAssetTool?["outputSchema"]?["properties"]?["derivatives"].Should().NotBeNull();
        getAssetTool?["outputSchema"]?["properties"]?["structuredResults"].Should().NotBeNull();
    }

    [Fact]
    public async Task Mcp_Asset_Tools_Should_Reuse_Asset_Read_Model_And_Hide_Storage_Internals()
    {
        var assetId = await UploadTestPngAsync("mcp-tool.png");

        var getAssetResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 11,
            method = "tools/call",
            @params = new
            {
                name = "get_asset",
                arguments = new
                {
                    id = assetId
                }
            }
        }, ProtocolVersion);

        getAssetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getAssetJson = await ReadJsonObjectAsync(getAssetResponse);
        getAssetJson["result"]?["isError"].Should().BeNull();

        var assetStructuredContent = getAssetJson["result"]?["structuredContent"]?.AsObject();
        assetStructuredContent.Should().NotBeNull();
        assetStructuredContent!["id"]?.GetValue<string>().Should().Be(assetId.ToString());
        assetStructuredContent["storageKey"].Should().BeNull();
        assetStructuredContent["storageProvider"].Should().BeNull();
        assetStructuredContent["derivatives"]?.AsArray().Should().NotBeEmpty();
        assetStructuredContent["structuredResults"]?.AsArray().Should().NotBeEmpty();

        var listAssetsResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 12,
            method = "tools/call",
            @params = new
            {
                name = "list_assets",
                arguments = new
                {
                    page = 1,
                    pageSize = 10
                }
            }
        }, ProtocolVersion);

        listAssetsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listAssetsJson = await ReadJsonObjectAsync(listAssetsResponse);
        var items = listAssetsJson["result"]?["structuredContent"]?["items"]?.AsArray();
        items.Should().NotBeNull();
        var listedAsset = items!.Single(node => node?["id"]?.GetValue<string>() == assetId.ToString());
        listedAsset?["storageProvider"].Should().BeNull();

        var contentUrlResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 13,
            method = "tools/call",
            @params = new
            {
                name = "get_asset_content_url",
                arguments = new
                {
                    id = assetId
                }
            }
        }, ProtocolVersion);

        contentUrlResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentUrlJson = await ReadJsonObjectAsync(contentUrlResponse);
        contentUrlJson["result"]?["structuredContent"]?["contentUrl"]?.GetValue<string>()
            .Should()
            .StartWith("http://test-server/content");

        var deleteResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 14,
            method = "tools/call",
            @params = new
            {
                name = "delete_asset",
                arguments = new
                {
                    id = assetId
                }
            }
        }, ProtocolVersion);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deleteJson = await ReadJsonObjectAsync(deleteResponse);
        deleteJson["result"]?["structuredContent"]?["status"]?.GetValue<string>().Should().Be("deleted");

        var getAfterDeleteResponse = await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id = 15,
            method = "tools/call",
            @params = new
            {
                name = "get_asset",
                arguments = new
                {
                    id = assetId
                }
            }
        }, ProtocolVersion);

        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getAfterDeleteJson = await ReadJsonObjectAsync(getAfterDeleteResponse);
        getAfterDeleteJson["result"]?["isError"]?.GetValue<bool>().Should().BeTrue();
        getAfterDeleteJson["result"]?["structuredContent"]?["error"]?["code"]?.GetValue<string>().Should().Be("asset_not_found");
    }

    [Fact]
    public async Task Mcp_Skill_Tools_Should_List_And_Run_Basic_Image_Enrich()
    {
        var assetId = await UploadTestPngAsync("mcp-skill-target.png");

        var listSkillsResponse = await PostToolCallAsync(
            id: 16,
            name: "list_skills",
            arguments: new { });

        listSkillsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listSkillsJson = await ReadJsonObjectAsync(listSkillsResponse);
        listSkillsJson["result"]?["isError"].Should().BeNull();

        var skills = listSkillsJson["result"]?["structuredContent"]?["skills"]?.AsArray();
        skills.Should().NotBeNull();
        var basicSkill = skills!.Single(node => node?["skillName"]?.GetValue<string>() == "basic_image_enrich");
        basicSkill.Should().NotBeNull();
        basicSkill!["description"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        basicSkill["steps"]?.AsArray()
            .Select(node => node?.GetValue<string>())
            .Should()
            .Contain(["generate_thumbnail", "generate_basic_caption"]);

        var runSkillResponse = await PostToolCallAsync(
            id: 17,
            name: "run_asset_skill",
            arguments: new
            {
                assetId,
                skillName = "basic_image_enrich"
            });

        runSkillResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var runSkillJson = await ReadJsonObjectAsync(runSkillResponse);
        runSkillJson["result"]?["isError"].Should().BeNull();
        runSkillJson["result"]?["structuredContent"]?["succeeded"]?.GetValue<bool>().Should().BeTrue();
        runSkillJson["result"]?["structuredContent"]?["skillName"]?.GetValue<string>().Should().Be("basic_image_enrich");

        var runSteps = runSkillJson["result"]?["structuredContent"]?["steps"]?.AsArray();
        runSteps.Should().NotBeNull();
        runSteps!
            .Select(node => node?["name"]?.GetValue<string>())
            .Should()
            .Contain(["generate_thumbnail", "generate_basic_caption"]);
        runSteps!
            .Select(node => node?["succeeded"]?.GetValue<bool>())
            .All(value => value is true)
            .Should()
            .BeTrue();

        var asset = runSkillJson["result"]?["structuredContent"]?["asset"]?.AsObject();
        asset.Should().NotBeNull();
        asset!["id"]?.GetValue<string>().Should().Be(assetId.ToString());
        asset["derivatives"]?.AsArray().Should().NotBeEmpty();
        asset["structuredResults"]?.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Mcp_Run_Asset_Skill_Should_Return_Skill_Not_Found()
    {
        var assetId = await UploadTestPngAsync("mcp-skill-missing.png");

        var runSkillResponse = await PostToolCallAsync(
            id: 18,
            name: "run_asset_skill",
            arguments: new
            {
                assetId,
                skillName = "unknown_skill"
            });

        runSkillResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var runSkillJson = await ReadJsonObjectAsync(runSkillResponse);
        runSkillJson["result"]?["isError"]?.GetValue<bool>().Should().BeTrue();
        runSkillJson["result"]?["structuredContent"]?["error"]?["code"]?.GetValue<string>().Should().Be("skill_not_found");
    }

    [Fact]
    public async Task Mcp_Upload_Asset_Should_Create_Asset_With_Derivatives_And_Structured_Results()
    {
        var pngBytes = CreatePngBytes(1, 1);
        var uploadResponse = await PostToolCallAsync(
            id: 21,
            name: "upload_asset",
            arguments: new
            {
                fileName = "mcp-upload.png",
                contentType = "image/png",
                contentBase64 = Convert.ToBase64String(pngBytes),
                description = "from-mcp-upload",
                altText = "mcp-alt-text"
            });

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadJson = await ReadJsonObjectAsync(uploadResponse);
        uploadJson["result"]?["isError"].Should().BeNull();

        var structuredContent = uploadJson["result"]?["structuredContent"]?.AsObject();
        structuredContent.Should().NotBeNull();
        structuredContent!["originalFileName"]?.GetValue<string>().Should().Be("mcp-upload.png");
        structuredContent["contentType"]?.GetValue<string>().Should().Be("image/png");
        structuredContent["width"]?.GetValue<int>().Should().Be(1);
        structuredContent["height"]?.GetValue<int>().Should().Be(1);
        structuredContent["description"]?.GetValue<string>().Should().Be("from-mcp-upload");
        structuredContent["altText"]?.GetValue<string>().Should().Be("mcp-alt-text");
        structuredContent["storageProvider"].Should().BeNull();
        structuredContent["storageKey"].Should().BeNull();

        var checksum = structuredContent["checksumSha256"]?.GetValue<string>();
        checksum.Should().NotBeNullOrWhiteSpace();
        Regex.IsMatch(checksum!, "^[0-9a-f]{64}$").Should().BeTrue();

        structuredContent["derivatives"]?.AsArray().Should().NotBeEmpty();
        structuredContent["structuredResults"]?.AsArray().Should().NotBeEmpty();

        var assetId = Guid.Parse(structuredContent["id"]!.GetValue<string>());
        var cleanupResponse = await PostToolCallAsync(
            id: 22,
            name: "delete_asset",
            arguments: new
            {
                id = assetId
            });

        cleanupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Mcp_Upload_Asset_Should_Return_Error_For_Invalid_Base64()
    {
        var response = await PostToolCallAsync(
            id: 31,
            name: "upload_asset",
            arguments: new
            {
                fileName = "invalid-base64.png",
                contentType = "image/png",
                contentBase64 = "###not-base64###"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        json["result"]?["isError"]?.GetValue<bool>().Should().BeTrue();
        json["result"]?["structuredContent"]?["error"]?["code"]?.GetValue<string>().Should().Be("asset_base64_invalid");
    }

    [Fact]
    public async Task Mcp_Upload_Asset_Should_Return_Error_For_Missing_FileName()
    {
        var response = await PostToolCallAsync(
            id: 32,
            name: "upload_asset",
            arguments: new
            {
                fileName = "",
                contentType = "image/png",
                contentBase64 = Convert.ToBase64String(CreatePngBytes(1, 1))
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        json["result"]?["isError"]?.GetValue<bool>().Should().BeTrue();
        json["result"]?["structuredContent"]?["error"]?["code"]?.GetValue<string>().Should().Be("asset_filename_invalid");
    }

    [Fact]
    public async Task Mcp_Upload_Asset_Should_Return_Error_For_Missing_ContentType()
    {
        var response = await PostToolCallAsync(
            id: 33,
            name: "upload_asset",
            arguments: new
            {
                fileName = "missing-content-type.png",
                contentType = "",
                contentBase64 = Convert.ToBase64String(CreatePngBytes(1, 1))
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        json["result"]?["isError"]?.GetValue<bool>().Should().BeTrue();
        json["result"]?["structuredContent"]?["error"]?["code"]?.GetValue<string>().Should().Be("asset_content_type_missing");
    }

    [Fact]
    public async Task Mcp_Upload_Asset_Should_Return_Error_For_Too_Large_File()
    {
        var tooLarge = new byte[10 * 1024 * 1024 + 1];
        var response = await PostToolCallAsync(
            id: 34,
            name: "upload_asset",
            arguments: new
            {
                fileName = "too-large.png",
                contentType = "image/png",
                contentBase64 = Convert.ToBase64String(tooLarge)
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        json["result"]?["isError"]?.GetValue<bool>().Should().BeTrue();
        json["result"]?["structuredContent"]?["error"]?["code"]?.GetValue<string>().Should().Be("asset_file_too_large");
    }

    [Fact]
    public async Task Mcp_Patch_Asset_Should_Update_Metadata_With_Null_Clear_Semantics()
    {
        var assetId = await UploadTestPngAsync("mcp-patch-source.png");

        var response = await PostToolCallAsync(
            id: 41,
            name: "patch_asset",
            arguments: new
            {
                id = assetId,
                description = "patched-from-mcp",
                altText = (string?)null,
                originalFileName = "patched-from-mcp.png"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        json["result"]?["isError"].Should().BeNull();

        var asset = json["result"]?["structuredContent"]?.AsObject();
        asset.Should().NotBeNull();
        asset!["id"]?.GetValue<string>().Should().Be(assetId.ToString());
        asset["description"]?.GetValue<string>().Should().Be("patched-from-mcp");
        asset["altText"].Should().BeNull();
        asset["originalFileName"]?.GetValue<string>().Should().Be("patched-from-mcp.png");
    }

    [Fact]
    public async Task Mcp_Batch_Delete_Assets_Should_Report_Deleted_And_NotFound_Items()
    {
        var assetId1 = await UploadTestPngAsync("mcp-batch-delete-1.png");
        var assetId2 = await UploadTestPngAsync("mcp-batch-delete-2.png");
        var missingId = Guid.NewGuid();

        var response = await PostToolCallAsync(
            id: 42,
            name: "batch_delete_assets",
            arguments: new
            {
                ids = new[] { assetId1.ToString(), assetId2.ToString(), missingId.ToString() }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonObjectAsync(response);
        json["result"]?["isError"].Should().BeNull();
        json["result"]?["structuredContent"]?["requestedCount"]?.GetValue<int>().Should().Be(3);
        json["result"]?["structuredContent"]?["deletedCount"]?.GetValue<int>().Should().Be(2);

        var notFoundIds = json["result"]?["structuredContent"]?["notFoundIds"]?.AsArray();
        notFoundIds.Should().NotBeNull();
        notFoundIds!.Select(node => node?.GetValue<string>()).Should().Contain(missingId.ToString());
    }

    [Fact]
    public async Task Mcp_Get_Asset_Usage_Stats_Should_Reflect_Newly_Uploaded_Asset()
    {
        var beforeResponse = await PostToolCallAsync(
            id: 43,
            name: "get_asset_usage_stats",
            arguments: new { });

        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeJson = await ReadJsonObjectAsync(beforeResponse);
        beforeJson["result"]?["isError"].Should().BeNull();
        var beforeTotalAssets = beforeJson["result"]?["structuredContent"]?["totalAssets"]?.GetValue<long>() ?? 0L;

        var assetId = await UploadTestPngAsync("mcp-usage-stats.png");
        assetId.Should().NotBeEmpty();

        var afterResponse = await PostToolCallAsync(
            id: 44,
            name: "get_asset_usage_stats",
            arguments: new { });

        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterJson = await ReadJsonObjectAsync(afterResponse);
        afterJson["result"]?["isError"].Should().BeNull();
        afterJson["result"]?["structuredContent"]?["totalAssets"]?.GetValue<long>().Should().Be(beforeTotalAssets + 1);
        afterJson["result"]?["structuredContent"]?["mostActiveSkill"]?["skillName"]?.GetValue<string>()
            .Should()
            .Be("basic_image_enrich");
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

    private async Task<HttpResponseMessage> PostToolCallAsync(int id, string name, object arguments)
    {
        return await PostMcpAsync(new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new
            {
                name,
                arguments
            }
        }, ProtocolVersion);
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

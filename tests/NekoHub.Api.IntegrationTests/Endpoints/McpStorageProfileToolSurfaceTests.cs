using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class McpStorageProfileToolSurfaceTests : IntegrationTestBase, IClassFixture<NekoHubApiKeyApplicationFactory>
{
    private const string ProtocolVersion = "2025-11-25";

    public McpStorageProfileToolSurfaceTests(NekoHubApiKeyApplicationFactory factory) : base(factory)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);
    }

    [Fact]
    public async Task Mcp_Storage_Profile_Tools_Should_Create_List_Update_And_Delete_Profiles_Without_Exposing_Secrets()
    {
        var createResponse = await PostToolCallAsync(
            id: 1001,
            name: "create_storage_profile",
            arguments: new
            {
                name = "mcp-s3-primary",
                displayName = "MCP S3 Primary",
                providerType = StorageProviderTypes.S3Compatible,
                isEnabled = true,
                configuration = new
                {
                    providerName = "s3",
                    endpoint = "http://minio.internal:9000",
                    bucket = "mcp-bucket",
                    region = "us-east-1",
                    forcePathStyle = true,
                    publicBaseUrl = "https://cdn.example.com/assets"
                },
                secretConfiguration = new
                {
                    accessKey = "ACCESSKEY1",
                    secretKey = "very-secret-value"
                }
            });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createRaw = await createResponse.Content.ReadAsStringAsync();
        createRaw.Should().NotContain("ACCESSKEY1");
        createRaw.Should().NotContain("very-secret-value");

        var createJson = ParseJsonObject(createRaw);
        createJson["result"]?["isError"].Should().BeNull();
        var created = createJson["result"]?["structuredContent"]?.AsObject();
        created.Should().NotBeNull();
        var profileId = Guid.Parse(created!["id"]!.GetValue<string>());
        created["providerType"]?.GetValue<string>().Should().Be(StorageProviderTypes.S3Compatible);
        created["configurationSummary"]?["endpointHost"]?.GetValue<string>().Should().Be("minio.internal:9000");
        created["configurationSummary"]?["bucketOrContainer"]?.GetValue<string>().Should().Be("mcp-bucket");
        created["configurationSummary"]?["region"]?.GetValue<string>().Should().Be("us-east-1");
        created["secretConfiguration"].Should().BeNull();

        var listResponse = await PostToolCallAsync(
            id: 1002,
            name: "list_storage_profiles",
            arguments: new { });

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listRaw = await listResponse.Content.ReadAsStringAsync();
        listRaw.Should().NotContain("ACCESSKEY1");
        listRaw.Should().NotContain("very-secret-value");

        var listJson = ParseJsonObject(listRaw);
        var listed = listJson["result"]?["structuredContent"]?["profiles"]?.AsArray()
            ?.Single(node => node?["id"]?.GetValue<string>() == profileId.ToString());
        listed.Should().NotBeNull();
        listed!["name"]?.GetValue<string>().Should().Be("mcp-s3-primary");
        listed["isDefault"]?.GetValue<bool>().Should().BeFalse();

        var updateResponse = await PostToolCallAsync(
            id: 1003,
            name: "update_storage_profile",
            arguments: new
            {
                id = profileId,
                displayName = "MCP S3 Updated",
                configuration = new
                {
                    providerName = "s3",
                    endpoint = "http://minio-2.internal:9000",
                    bucket = "mcp-bucket-updated",
                    region = "ap-southeast-1",
                    forcePathStyle = false,
                    publicBaseUrl = "https://cdn.example.com/updated"
                },
                secretConfiguration = new
                {
                    accessKey = "ACCESSKEY2",
                    secretKey = "rotated-secret"
                },
                setAsDefault = true
            });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateRaw = await updateResponse.Content.ReadAsStringAsync();
        updateRaw.Should().NotContain("ACCESSKEY2");
        updateRaw.Should().NotContain("rotated-secret");

        var updateJson = ParseJsonObject(updateRaw);
        var updated = updateJson["result"]?["structuredContent"]?.AsObject();
        updated.Should().NotBeNull();
        updated!["displayName"]?.GetValue<string>().Should().Be("MCP S3 Updated");
        updated["isDefault"]?.GetValue<bool>().Should().BeTrue();
        updated["configurationSummary"]?["endpointHost"]?.GetValue<string>().Should().Be("minio-2.internal:9000");
        updated["configurationSummary"]?["bucketOrContainer"]?.GetValue<string>().Should().Be("mcp-bucket-updated");
        updated["configurationSummary"]?["region"]?.GetValue<string>().Should().Be("ap-southeast-1");
        updated["configurationSummary"]?["forcePathStyle"]?.GetValue<bool>().Should().BeFalse();

        var deleteResponse = await PostToolCallAsync(
            id: 1004,
            name: "delete_storage_profile",
            arguments: new
            {
                id = profileId
            });

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deleteJson = await ReadJsonObjectAsync(deleteResponse);
        deleteJson["result"]?["isError"].Should().BeNull();
        deleteJson["result"]?["structuredContent"]?["id"]?.GetValue<string>().Should().Be(profileId.ToString());
        deleteJson["result"]?["structuredContent"]?["wasDefault"]?.GetValue<bool>().Should().BeTrue();
        deleteJson["result"]?["structuredContent"]?["status"]?.GetValue<string>().Should().Be("deleted");

        var listAfterDeleteResponse = await PostToolCallAsync(
            id: 1005,
            name: "list_storage_profiles",
            arguments: new { });

        listAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listAfterDeleteJson = await ReadJsonObjectAsync(listAfterDeleteResponse);
        listAfterDeleteJson["result"]?["structuredContent"]?["profiles"]?.AsArray()
            ?.Select(node => node?["id"]?.GetValue<string>())
            .Should()
            .NotContain(profileId.ToString());
    }

    [Fact]
    public async Task Mcp_Upload_Asset_Should_Respect_Explicit_Storage_Profile_Id()
    {
        var createProfileResponse = await PostToolCallAsync(
            id: 1101,
            name: "create_storage_profile",
            arguments: new
            {
                name = "mcp-local-explicit",
                providerType = StorageProviderTypes.Local,
                isEnabled = true,
                configuration = new
                {
                    rootPath = "storage/mcp-local-explicit",
                    createDirectoryIfMissing = true,
                    publicBaseUrl = "http://test-server/content"
                }
            });

        createProfileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createProfileJson = await ReadJsonObjectAsync(createProfileResponse);
        var profileId = Guid.Parse(createProfileJson["result"]?["structuredContent"]?["id"]!.GetValue<string>()!);

        var uploadResponse = await PostToolCallAsync(
            id: 1102,
            name: "upload_asset",
            arguments: new
            {
                fileName = "mcp-profile-bound.png",
                contentType = "image/png",
                contentBase64 = Convert.ToBase64String(CreatePngBytes(1, 1)),
                storageProviderProfileId = profileId
            });

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadJson = await ReadJsonObjectAsync(uploadResponse);
        uploadJson["result"]?["isError"].Should().BeNull();

        var assetId = Guid.Parse(uploadJson["result"]?["structuredContent"]?["id"]!.GetValue<string>()!);
        var detailResponse = await Client.GetAsync($"/api/v1/assets/{assetId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.StorageProviderProfileId.Should().Be(profileId);
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
        return ParseJsonObject(raw);
    }

    private static JsonObject ParseJsonObject(string raw)
    {
        var node = JsonNode.Parse(raw);
        node.Should().NotBeNull();
        node.Should().BeOfType<JsonObject>();
        return (JsonObject)node!;
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(120, 180, 255, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}

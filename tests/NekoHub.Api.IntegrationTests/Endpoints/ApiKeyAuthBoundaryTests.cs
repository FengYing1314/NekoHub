using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class ApiKeyAuthBoundaryTests : IntegrationTestBase, IClassFixture<NekoHubApiKeyApplicationFactory>
{
    public ApiKeyAuthBoundaryTests(NekoHubApiKeyApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Assets_Without_ApiKey_Should_Return_401_With_Missing_Code()
    {
        var response = await Client.GetAsync("/api/v1/assets?page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle();

        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("api_key_missing");
    }

    [Fact]
    public async Task Assets_With_Invalid_ApiKey_Should_Return_401_With_Invalid_Code()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/assets?page=1&pageSize=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-key");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("api_key_invalid");
    }

    [Fact]
    public async Task Assets_And_Mcp_With_Valid_ApiKey_Should_Pass()
    {
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/assets?page=1&pageSize=1");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);
        var listResponse = await Client.SendAsync(listRequest);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var mcpRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-11-25",
                    clientInfo = new
                    {
                        name = "auth-test",
                        version = "1.0.0"
                    },
                    capabilities = new { }
                }
            })
        };
        mcpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);
        mcpRequest.Headers.Accept.ParseAdd("application/json");

        var mcpResponse = await Client.SendAsync(mcpRequest);
        mcpResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task System_Storage_Providers_Should_Require_ApiKey()
    {
        var response = await Client.GetAsync("/api/v1/system/storage/providers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("api_key_missing");
    }

    [Fact]
    public async Task System_Storage_Provider_Create_Should_Require_ApiKey()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = "local-test",
            providerType = "local",
            configuration = new
            {
                rootPath = "storage/assets"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("api_key_missing");
    }

    [Fact]
    public async Task System_Ping_Should_Remain_Anonymous()
    {
        var response = await Client.GetAsync("/api/v1/system/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<object>(response);
        payload.Should().NotBeNull();
    }
}

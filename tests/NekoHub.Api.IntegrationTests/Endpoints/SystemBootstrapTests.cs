using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class SystemBootstrapTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Bootstrap_Should_Report_Disabled_ApiKey_In_Default_Testing_Environment()
    {
        using var factory = new NekoHubApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/bootstrap");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<SystemBootstrapResponse>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Data.ApiKeyRequired.Should().BeFalse();
        payload.Data.MaxUploadSizeBytes.Should().Be(10 * 1024 * 1024);
        payload.Data.AllowedContentTypes.Should().Contain("image/png");
    }

    [Fact]
    public async Task Bootstrap_Should_Report_Enabled_ApiKey_When_Auth_Is_Enabled()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/bootstrap");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<SystemBootstrapResponse>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Data.ApiKeyRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Bootstrap_Should_Return_Deduplicated_Allowed_Content_Types()
    {
        using var factory = new NekoHubDuplicateAssetOptionsApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/bootstrap");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<SystemBootstrapResponse>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Data.AllowedContentTypes.Should().Equal("image/jpeg", "image/png", "image/webp", "image/gif");
    }
}

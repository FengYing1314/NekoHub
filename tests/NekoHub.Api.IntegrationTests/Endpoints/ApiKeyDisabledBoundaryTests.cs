using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using NekoHub.Api.IntegrationTests.Setup;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class ApiKeyDisabledBoundaryTests : IntegrationTestBase, IClassFixture<NekoHubJwtOnlyAnonymousApplicationFactory>
{
    public ApiKeyDisabledBoundaryTests(NekoHubJwtOnlyAnonymousApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Management_Endpoint_With_Opaque_Bearer_Should_Not_Be_Treated_As_ApiKey_Admin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/storage/providers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "opaque-token");

        var response = await Client.SendAsync(request);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}

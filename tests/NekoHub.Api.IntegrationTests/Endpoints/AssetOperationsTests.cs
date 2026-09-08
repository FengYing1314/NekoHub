using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Storage;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public sealed class AssetOperationsTests(NekoHubApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Asset_Operator_Can_Read_Execution_Options_Without_System_Permissions()
    {
        var username = $"operator-{Guid.NewGuid():N}";
        var created = await Client.PostAsJsonAsync("/api/v1/users", new
        {
            username, password = "operator-password-123", role = "user", isActive = true,
            permissions = new[] { "assets.read", "assets.create", "assets.update" }
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        using var user = Factory.CreateAnonymousClient();
        var login = await user.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "operator-password-123" });
        var session = await GetResponseDataAsync<AuthTokenResponse>(login);
        user.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);

        (await user.GetAsync("/api/v1/system/workflows")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await user.GetAsync("/api/v1/system/storage/providers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var workflows = await user.GetAsync("/api/v1/assets/workflows");
        workflows.StatusCode.Should().Be(HttpStatusCode.OK);
        (await workflows.Content.ReadAsStringAsync()).Should().NotContain("graphJson");
        var targets = await user.GetAsync("/api/v1/assets/storage-targets");
        targets.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await targets.Content.ReadAsStringAsync();
        json.Should().Contain("isDefault").And.NotContain("rootPath").And.NotContain("secretConfiguration");
        using var anonymous = Factory.CreateAnonymousClient();
        (await anonymous.GetAsync("/api/v1/assets/storage-targets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bound_Profile_Rejects_Location_Change_But_Allows_Display_Change()
    {
        var root = Path.Combine(Factory.TestStoragePath, Guid.NewGuid().ToString("N"));
        var response = await Client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = $"bound-{Guid.NewGuid():N}", providerType = StorageProviderTypes.Local,
            isEnabled = true, isDefault = false, configuration = new { rootPath = root }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var profile = (await GetResponseDataAsync<StorageProviderProfileResponse>(response))!;
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aL1sAAAAASUVORK5CYII="));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "bound.png");
        form.Add(new StringContent(profile.Id.ToString()), "storageProviderProfileId");
        form.Add(new StringContent("false"), "runEnrichment");
        form.Add(new StringContent("false"), "isPublic");
        var upload = await Client.PostAsync("/api/v1/assets", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset = (await GetResponseDataAsync<AssetResponse>(upload))!;

        var change = await Client.PatchAsJsonAsync($"/api/v1/system/storage/providers/{profile.Id}",
            new { configuration = new { rootPath = root + "-moved" } });
        change.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetErrorAsync(change))!.Code.Should().Be("storage_provider_profile_location_in_use");
        var rename = await Client.PatchAsJsonAsync($"/api/v1/system/storage/providers/{profile.Id}", new { displayName = "Renamed" });
        rename.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await Client.GetAsync($"/api/v1/assets/{asset.Id}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }
}

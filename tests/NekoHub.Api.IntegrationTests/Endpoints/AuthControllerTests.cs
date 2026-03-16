using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Users;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_Refresh_Logout_Rotation_Should_Work_EndToEnd()
    {
        using var anonymousClient = CreateAnonymousClient();

        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            NekoHubApplicationFactory.BootstrapAdminUsername,
            NekoHubApplicationFactory.BootstrapAdminPassword));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await GetResponseDataAsync<AuthTokenResponse>(loginResponse);
        session.Should().NotBeNull();
        session!.User.Role.Should().Be(UserRole.SuperAdmin);
        session.AccessToken.Should().NotBeNullOrWhiteSpace();
        session.RefreshToken.Should().NotBeNullOrWhiteSpace();

        anonymousClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var meResponse = await anonymousClient.GetAsync("/api/v1/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await GetResponseDataAsync<CurrentUserResponse>(meResponse);
        me.Should().NotBeNull();
        me!.Username.Should().Be(NekoHubApplicationFactory.BootstrapAdminUsername);
        me.Permissions.Should().Contain("users.managePermissions");

        var refreshResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(session.RefreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await GetResponseDataAsync<AuthTokenResponse>(refreshResponse);
        refreshed.Should().NotBeNull();
        refreshed!.RefreshToken.Should().NotBe(session.RefreshToken);

        var replayResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(session.RefreshToken));
        replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var replayError = await GetErrorAsync(replayResponse);
        replayError.Should().NotBeNull();
        replayError!.Code.Should().Be("auth_refresh_token_reused");

        anonymousClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        var logoutResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/logout", new RefreshTokenRequest(refreshed.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loggedOutRefreshResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(refreshed.RefreshToken));
        loggedOutRefreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Inactive_User_Should_Not_Be_Able_To_Login()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/v1/users", new
        {
            username = "inactive-user",
            password = "user-pass-123",
            role = "user",
            isActive = false,
            permissions = new[] { "assets.read" }
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var anonymousClient = CreateAnonymousClient();
        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("inactive-user", "user-pass-123"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await GetErrorAsync(loginResponse);
        error.Should().NotBeNull();
        error!.Code.Should().Be("auth_user_inactive");
    }

    [Fact]
    public async Task Admin_Should_Not_Be_Able_To_Create_Admin()
    {
        var createAdminResponse = await Client.PostAsJsonAsync("/api/v1/users", new
        {
            username = "limited-admin",
            password = "admin-pass-123",
            role = "admin",
            isActive = true
        });

        createAdminResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var adminClient = CreateAnonymousClient();
        var adminLoginResponse = await adminClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("limited-admin", "admin-pass-123"));
        adminLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminSession = await GetResponseDataAsync<AuthTokenResponse>(adminLoginResponse);
        adminSession.Should().NotBeNull();

        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminSession!.AccessToken);
        var forbiddenResponse = await adminClient.PostAsJsonAsync("/api/v1/users", new
        {
            username = "another-admin",
            password = "admin-pass-456",
            role = "admin",
            isActive = true
        });

        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

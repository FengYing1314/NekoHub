using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Users;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class UsersControllerTests : IntegrationTestBase
{
    public UsersControllerTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task List_And_GetById_When_Admin_Only_Should_Expose_Managed_User_Accounts()
    {
        var adminActor = await CreateUserAsync("admin-actor", "admin-pass-123", UserRole.Admin);
        var peerAdmin = await CreateUserAsync("peer-admin", "peer-admin-pass-123", UserRole.Admin);
        var managedUser = await CreateUserAsync("managed-user", "managed-user-pass-123", UserRole.User);

        using var adminClient = await LoginAsUserAsync(adminActor.Username, "admin-pass-123");

        var listResponse = await adminClient.GetAsync("/api/v1/users");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await GetResponseDataAsync<List<UserListItemResponse>>(listResponse);
        users.Should().NotBeNull();
        var userIds = users!.Select(static user => user.Id).ToList();
        userIds.Should().Contain(managedUser.Id);
        userIds.Should().NotContain([adminActor.Id, peerAdmin.Id]);

        var detailResponse = await adminClient.GetAsync($"/api/v1/users/{managedUser.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await GetResponseDataAsync<UserDetailResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(managedUser.Id);
        detail.Role.Should().Be(UserRole.User);

        var forbiddenResponse = await adminClient.GetAsync($"/api/v1/users/{peerAdmin.Id}");
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await GetErrorAsync(forbiddenResponse);
        error.Should().NotBeNull();
        error!.Code.Should().Be("user_manage_forbidden");
    }

    [Fact]
    public async Task Update_When_SuperAdmin_Changes_Username_And_Role_Should_Apply_Default_Admin_Permissions()
    {
        var originalUser = await CreateUserAsync("promote-target", "promote-target-pass-123", UserRole.User);
        var renamedUsername = UniqueName("renamed-admin");

        var response = await PatchJsonAsync(
            Client,
            $"/api/v1/users/{originalUser.Id}",
            $$"""
              {
                "username": "  {{renamedUsername}}  ",
                "role": "admin"
              }
              """);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await GetResponseDataAsync<UserDetailResponse>(response);
        updated.Should().NotBeNull();
        updated!.Username.Should().Be(renamedUsername);
        updated.Role.Should().Be(UserRole.Admin);
        updated.Permissions.Should().Contain("settings.update");
        updated.Permissions.Should().Contain("users.managePermissions");

        // 这里坚持走真实登录链路，避免把“改名成功但认证层没同步”的问题藏掉。
        using var renamedClient = await LoginAsUserAsync(renamedUsername, "promote-target-pass-123");
        var meResponse = await renamedClient.GetAsync("/api/v1/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await GetResponseDataAsync<CurrentUserResponse>(meResponse);
        me.Should().NotBeNull();
        me!.Username.Should().Be(renamedUsername);
        me.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task SetStatus_When_SuperAdmin_Disables_And_Reenables_User_Should_Control_Login_Access()
    {
        var managedUser = await CreateUserAsync("toggle-target", "toggle-target-pass-123", UserRole.User);

        var disableResponse = await Client.PostAsJsonAsync(
            $"/api/v1/users/{managedUser.Id}/status",
            new UpdateUserStatusRequest(false));
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var disabledUser = await GetResponseDataAsync<UserDetailResponse>(disableResponse);
        disabledUser.Should().NotBeNull();
        disabledUser!.IsActive.Should().BeFalse();

        using var oldPasswordClient = CreateAnonymousClient();
        var blockedLogin = await oldPasswordClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(managedUser.Username, "toggle-target-pass-123"));
        blockedLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var blockedLoginError = await GetErrorAsync(blockedLogin);
        blockedLoginError.Should().NotBeNull();
        blockedLoginError!.Code.Should().Be("auth_user_inactive");

        var enableResponse = await Client.PostAsJsonAsync(
            $"/api/v1/users/{managedUser.Id}/status",
            new UpdateUserStatusRequest(true));
        enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var enabledUser = await GetResponseDataAsync<UserDetailResponse>(enableResponse);
        enabledUser.Should().NotBeNull();
        enabledUser!.IsActive.Should().BeTrue();

        using var restoredClient = await LoginAsUserAsync(managedUser.Username, "toggle-target-pass-123");
        var meResponse = await restoredClient.GetAsync("/api/v1/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetStatus_When_Disabling_SuperAdmin_Should_Return_Forbidden()
    {
        var bootstrapUser = await GetCurrentUserAsync(Client);
        var superAdminDisableResponse = await Client.PostAsJsonAsync(
            $"/api/v1/users/{bootstrapUser.Id}/status",
            new UpdateUserStatusRequest(false));
        superAdminDisableResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var superAdminDisableError = await GetErrorAsync(superAdminDisableResponse);
        superAdminDisableError.Should().NotBeNull();
        superAdminDisableError!.Code.Should().Be("user_super_admin_disable_forbidden");
    }

    [Fact]
    public async Task Update_When_Admin_Promotes_User_Should_Return_Forbidden_Without_Changing_User()
    {
        var admin = await CreateUserAsync("restricted-admin", "admin-pass-123", UserRole.Admin,
            permissions: ["users.update"]);
        var target = await CreateUserAsync("promotion-target", "user-pass-123", UserRole.User);
        using var adminClient = await LoginAsUserAsync(admin.Username, "admin-pass-123");

        var response = await adminClient.PatchAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            role = "admin",
            username = UniqueName("unauthorized-rename")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await GetErrorAsync(response);
        error!.Code.Should().Be("user_role_change_forbidden");
        var detail = await GetResponseDataAsync<UserDetailResponse>(await Client.GetAsync($"/api/v1/users/{target.Id}"));
        detail!.Role.Should().Be(UserRole.User);
        detail.Username.Should().Be(target.Username);
        detail.Permissions.Should().Equal("assets.read");
    }

    [Fact]
    public async Task Demoted_Admin_Should_Not_Keep_Management_Identity_From_Existing_AccessToken()
    {
        var admin = await CreateUserAsync("demotion-admin", "admin-pass-123", UserRole.Admin);
        var target = await CreateUserAsync("demotion-target", "user-pass-123", UserRole.User);
        using var adminClient = await LoginAsUserAsync(admin.Username, "admin-pass-123");
        (await Client.PatchAsJsonAsync($"/api/v1/users/{admin.Id}", new { role = "user" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PatchAsJsonAsync($"/api/v1/users/{admin.Id}/permissions",
            new UpdateUserPermissionsRequest(["users.read"]))).StatusCode.Should().Be(HttpStatusCode.OK);

        // 保留读取权限以单独验证角色边界，防止测试仅因缺少权限而通过。
        (await adminClient.GetAsync($"/api/v1/users/{target.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetCurrentUserAsync(adminClient)).Role.Should().Be(UserRole.User);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_Status_Without_Disable_Permission_Should_Return_Forbidden(bool initiallyActive)
    {
        var admin = await CreateUserAsync("status-admin", "admin-pass-123", UserRole.Admin,
            permissions: ["users.update"]);
        var target = await CreateUserAsync("status-target", "user-pass-123", UserRole.User, initiallyActive);
        using var adminClient = await LoginAsUserAsync(admin.Username, "admin-pass-123");

        var response = await adminClient.PatchAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            isActive = !initiallyActive,
            username = UniqueName("unauthorized-rename")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var detail = await GetResponseDataAsync<UserDetailResponse>(await Client.GetAsync($"/api/v1/users/{target.Id}"));
        detail!.IsActive.Should().Be(initiallyActive);
        detail.Username.Should().Be(target.Username);
    }

    [Fact]
    public async Task Update_Status_With_Disable_Permission_Should_Apply()
    {
        var admin = await CreateUserAsync("status-admin", "admin-pass-123", UserRole.Admin,
            permissions: ["users.update", "users.disable"]);
        var target = await CreateUserAsync("status-target", "user-pass-123", UserRole.User);
        using var adminClient = await LoginAsUserAsync(admin.Username, "admin-pass-123");

        var response = await adminClient.PatchAsJsonAsync($"/api/v1/users/{target.Id}", new { isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await GetResponseDataAsync<UserDetailResponse>(response);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Update_When_Disabling_SuperAdmin_Should_Return_Forbidden()
    {
        var superAdmin = await GetCurrentUserAsync(Client);

        var response = await Client.PatchAsJsonAsync($"/api/v1/users/{superAdmin.Id}", new { isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await GetErrorAsync(response);
        error!.Code.Should().Be("user_super_admin_disable_forbidden");
        (await GetCurrentUserAsync(Client)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_When_SuperAdmin_Changes_UserPassword_Should_Switch_Valid_Credentials()
    {
        var managedUser = await CreateUserAsync("password-target", "old-password-123", UserRole.User);
        using var existingClient = CreateAnonymousClient();
        var initialSession = await GetResponseDataAsync<AuthTokenResponse>(await existingClient.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(managedUser.Username, "old-password-123")));
        var rotatedSession = await GetResponseDataAsync<AuthTokenResponse>(await existingClient.PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshTokenRequest(initialSession!.RefreshToken)));
        var otherSession = await GetResponseDataAsync<AuthTokenResponse>(await existingClient.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(managedUser.Username, "old-password-123")));

        var resetResponse = await Client.PostAsJsonAsync(
            $"/api/v1/users/{managedUser.Id}/reset-password",
            new ResetUserPasswordRequest("new-password-123"));
        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 重置必须撤销全部设备与轮换前的 access token，而不只是阻止旧密码重新登录。
        foreach (var session in new[] { initialSession, rotatedSession, otherSession })
        {
            existingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
            (await existingClient.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        foreach (var session in new[] { rotatedSession, otherSession })
        {
            (await existingClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(session!.RefreshToken)))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var oldPasswordClient = CreateAnonymousClient();
        var oldPasswordLogin = await oldPasswordClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(managedUser.Username, "old-password-123"));
        oldPasswordLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var oldPasswordError = await GetErrorAsync(oldPasswordLogin);
        oldPasswordError.Should().NotBeNull();
        oldPasswordError!.Code.Should().Be("auth_invalid_credentials");

        using var updatedPasswordClient = await LoginAsUserAsync(managedUser.Username, "new-password-123");
        var meResponse = await updatedPasswordClient.GetAsync("/api/v1/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdatePermissions_When_SuperAdmin_Changes_Target_Should_Store_Normalized_Permissions()
    {
        var managedUser = await CreateUserAsync("permission-target", "permission-target-pass-123", UserRole.User);

        var response = await Client.PatchAsJsonAsync(
            $"/api/v1/users/{managedUser.Id}/permissions",
            new UpdateUserPermissionsRequest([" users.read ", "assets.read", "users.read", "unknown.permission"]));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await GetResponseDataAsync<UserDetailResponse>(response);
        updated.Should().NotBeNull();
        updated!.Permissions.Should().Equal("assets.read", "users.read");
    }

    [Fact]
    public async Task UpdatePermissions_When_Editing_SuperAdmin_Should_Return_Forbidden()
    {
        var bootstrapUser = await GetCurrentUserAsync(Client);

        var response = await Client.PatchAsJsonAsync(
            $"/api/v1/users/{bootstrapUser.Id}/permissions",
            new UpdateUserPermissionsRequest(["assets.read"]));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("user_super_admin_permissions_forbidden");
    }

    private async Task<UserDetailResponse> CreateUserAsync(
        string usernamePrefix,
        string password,
        UserRole role,
        bool? isActive = null,
        IReadOnlyList<string>? permissions = null)
    {
        var username = UniqueName(usernamePrefix);
        var response = await Client.PostAsJsonAsync("/api/v1/users", new
        {
            username,
            password,
            role = role switch
            {
                UserRole.Admin => "admin",
                UserRole.SuperAdmin => "superAdmin",
                _ => "user"
            },
            isActive,
            permissions
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await GetResponseDataAsync<UserDetailResponse>(response);
        created.Should().NotBeNull();
        return created!;
    }

    private async Task<HttpClient> LoginAsUserAsync(string username, string password)
    {
        // 所有权限/口令类测试都通过真实登录拿 token，避免测试只证明 service 正常而漏掉认证集成问题。
        var client = CreateAnonymousClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await GetResponseDataAsync<AuthTokenResponse>(loginResponse);
        session.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private async Task<CurrentUserResponse> GetCurrentUserAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var currentUser = await GetResponseDataAsync<CurrentUserResponse>(response);
        currentUser.Should().NotBeNull();
        return currentUser!;
    }

    private static async Task<HttpResponseMessage> PatchJsonAsync(HttpClient client, string path, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PatchAsync(path, content);
    }

    private static string UniqueName(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}".ToLowerInvariant();
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Storage;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class SystemStorageProviderManagementTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Create_Local_Profile_Should_Succeed()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = "local-primary",
            displayName = "Local Primary",
            providerType = StorageProviderTypes.Local,
            isEnabled = true,
            isDefault = true,
            configuration = new
            {
                rootPath = "storage/assets",
                createDirectoryIfMissing = true,
                publicBaseUrl = "http://test-server/content"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await GetResponseDataAsync<StorageProviderProfileResponse>(response);
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("local-primary");
        payload.ProviderType.Should().Be(StorageProviderTypes.Local);
        payload.IsDefault.Should().BeTrue();
        payload.ConfigurationSummary.RootPath.Should().Be("storage/assets");
        payload.ConfigurationSummary.PublicBaseUrl.Should().Be("http://test-server/content");
        payload.Capabilities.RequiresAccessProxy.Should().BeTrue();
    }

    [Fact]
    public async Task Create_S3_Profile_Should_Succeed_And_Not_Expose_Secret()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = "s3-primary",
            displayName = "S3 Primary",
            providerType = StorageProviderTypes.S3Compatible,
            isEnabled = true,
            configuration = new
            {
                providerName = "s3",
                endpoint = "http://minio.internal:9000",
                bucket = "nekohub",
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

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await GetResponseDataAsync<StorageProviderProfileResponse>(response);
        payload.Should().NotBeNull();
        payload!.ProviderType.Should().Be(StorageProviderTypes.S3Compatible);
        payload.ConfigurationSummary.EndpointHost.Should().Be("minio.internal:9000");
        payload.ConfigurationSummary.BucketOrContainer.Should().Be("nekohub");
        payload.ConfigurationSummary.Region.Should().Be("us-east-1");
        payload.Capabilities.SupportsDirectPublicUrl.Should().BeTrue();

        var rawCreateJson = await response.Content.ReadAsStringAsync();
        rawCreateJson.Should().NotContain("ACCESSKEY1");
        rawCreateJson.Should().NotContain("very-secret-value");

        var overview = await GetOverviewAsync(client);
        var rawOverview = await (await client.GetAsync("/api/v1/system/storage/providers")).Content.ReadAsStringAsync();
        overview.Profiles.Should().ContainSingle();
        rawOverview.Should().NotContain("ACCESSKEY1");
        rawOverview.Should().NotContain("very-secret-value");
    }

    [Fact]
    public async Task Create_GitHub_Releases_Profile_Should_Succeed_And_Not_Expose_Token()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = "gh-releases-main",
            displayName = "GitHub Releases",
            providerType = StorageProviderTypes.GitHubReleases,
            isEnabled = true,
            configuration = new
            {
                owner = "nekohub",
                repo = "assets",
                releaseTagMode = "fixed",
                fixedTag = "v1.0.0",
                assetPathPrefix = "images/public",
                visibilityPolicy = "public-first",
                allowDelete = false
            },
            secretConfiguration = new
            {
                token = "ghp_release_token_value"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await GetResponseDataAsync<StorageProviderProfileResponse>(response);
        payload.Should().NotBeNull();
        payload!.ProviderType.Should().Be(StorageProviderTypes.GitHubReleases);
        payload.ConfigurationSummary.Owner.Should().Be("nekohub");
        payload.ConfigurationSummary.Repository.Should().Be("assets");
        payload.ConfigurationSummary.ReleaseTagMode.Should().Be("fixed");
        payload.ConfigurationSummary.FixedTag.Should().Be("v1.0.0");
        payload.ConfigurationSummary.PathPrefix.Should().Be("images/public");
        payload.ConfigurationSummary.VisibilityPolicy.Should().Be("public-first");
        payload.ConfigurationSummary.AssetPathPrefix.Should().Be("images/public");
        payload.Capabilities.SupportsPublicRead.Should().BeTrue();
        payload.Capabilities.SupportsPrivateRead.Should().BeFalse();
        payload.Capabilities.SupportsVisibilityToggle.Should().BeFalse();
        payload.Capabilities.SupportsDelete.Should().BeFalse();
        payload.Capabilities.RecommendedForPrimaryStorage.Should().BeFalse();
        payload.Capabilities.IsPlatformBacked.Should().BeTrue();
        payload.Capabilities.IsExperimental.Should().BeTrue();
        payload.Capabilities.RequiresTokenForPrivateRead.Should().BeFalse();

        var rawCreateJson = await response.Content.ReadAsStringAsync();
        rawCreateJson.Should().NotContain("ghp_release_token_value");

        var overviewResponse = await client.GetAsync("/api/v1/system/storage/providers");
        var rawOverviewJson = await overviewResponse.Content.ReadAsStringAsync();
        rawOverviewJson.Should().NotContain("ghp_release_token_value");
    }

    [Fact]
    public async Task Create_GitHub_Repo_Profile_Should_Succeed_And_Not_Expose_Token()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = "gh-repo-main",
            displayName = "GitHub Repo",
            providerType = StorageProviderTypes.GitHubRepo,
            isEnabled = true,
            configuration = new
            {
                owner = "nekohub",
                repo = "assets-repo",
                @ref = "main",
                basePath = "media/images",
                visibilityPolicy = "public-only",
                apiBaseUrl = "https://api.github.com",
                rawBaseUrl = "https://raw.githubusercontent.com",
                allowDelete = false
            },
            secretConfiguration = new
            {
                token = "ghp_repo_token_value"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await GetResponseDataAsync<StorageProviderProfileResponse>(response);
        payload.Should().NotBeNull();
        payload!.ProviderType.Should().Be(StorageProviderTypes.GitHubRepo);
        payload.ConfigurationSummary.Owner.Should().Be("nekohub");
        payload.ConfigurationSummary.Repository.Should().Be("assets-repo");
        payload.ConfigurationSummary.Reference.Should().Be("main");
        payload.ConfigurationSummary.PathPrefix.Should().Be("media/images");
        payload.ConfigurationSummary.VisibilityPolicy.Should().Be("public-only");
        payload.ConfigurationSummary.BasePath.Should().Be("media/images");
        payload.ConfigurationSummary.ApiBaseUrl.Should().Be("https://api.github.com");
        payload.ConfigurationSummary.RawBaseUrl.Should().Be("https://raw.githubusercontent.com");
        payload.Capabilities.RequiresAccessProxy.Should().BeTrue();
        payload.Capabilities.SupportsPrivateRead.Should().BeTrue();
        payload.Capabilities.SupportsDelete.Should().BeTrue();
        payload.Capabilities.RecommendedForPrimaryStorage.Should().BeFalse();
        payload.Capabilities.IsPlatformBacked.Should().BeTrue();
        payload.Capabilities.IsExperimental.Should().BeTrue();
        payload.Capabilities.RequiresTokenForPrivateRead.Should().BeTrue();

        var rawCreateJson = await response.Content.ReadAsStringAsync();
        rawCreateJson.Should().NotContain("ghp_repo_token_value");

        var overviewResponse = await client.GetAsync("/api/v1/system/storage/providers");
        var rawOverviewJson = await overviewResponse.Content.ReadAsStringAsync();
        rawOverviewJson.Should().NotContain("ghp_repo_token_value");
    }

    [Fact]
    public async Task Create_Local_Profile_Should_Fail_When_RootPath_Missing()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = "local-invalid",
            providerType = StorageProviderTypes.Local,
            isEnabled = true,
            configuration = new
            {
                publicBaseUrl = "http://test-server/content"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_root_path_required");
    }

    [Fact]
    public async Task Create_S3_Profile_Should_Fail_When_Secret_Missing()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = "s3-invalid",
            providerType = StorageProviderTypes.S3Compatible,
            isEnabled = true,
            configuration = new
            {
                endpoint = "http://minio.internal:9000",
                bucket = "nekohub",
                region = "us-east-1",
                forcePathStyle = true
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_access_key_required");
    }

    [Fact]
    public async Task Create_GitHub_Releases_Profile_Should_Fail_When_Fixed_Tag_Missing()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = "gh-releases-invalid",
            providerType = StorageProviderTypes.GitHubReleases,
            isEnabled = true,
            configuration = new
            {
                owner = "nekohub",
                repo = "assets",
                releaseTagMode = "fixed"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_github_releases_fixed_tag_required");
    }

    [Fact]
    public async Task Create_GitHub_Repo_Profile_Should_Fail_When_Private_Token_Policy_Has_No_Token()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name = "gh-repo-private-invalid",
            providerType = StorageProviderTypes.GitHubRepo,
            isEnabled = true,
            configuration = new
            {
                owner = "nekohub",
                repo = "assets-private",
                @ref = "main",
                basePath = "media/private",
                visibilityPolicy = "private-token",
                apiBaseUrl = "https://api.github.com",
                rawBaseUrl = "https://raw.githubusercontent.com",
                allowDelete = false
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_github_repo_token_required_for_private");
    }

    [Fact]
    public async Task Update_Profile_Should_Succeed_And_Not_Expose_Updated_Secret()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var created = await CreateS3ProfileAsync(client, "s3-updatable");

        var response = await client.PatchAsJsonAsync($"/api/v1/system/storage/providers/{created.Id}", new
        {
            displayName = "S3 Updated",
            configuration = new
            {
                providerName = "s3",
                endpoint = "http://minio-2.internal:9000",
                bucket = "nekohub-updated",
                region = "ap-southeast-1",
                forcePathStyle = false,
                publicBaseUrl = "https://cdn.example.com/updated"
            },
            secretConfiguration = new
            {
                accessKey = "ACCESSKEY2",
                secretKey = "rotated-secret"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<StorageProviderProfileResponse>(response);
        payload.Should().NotBeNull();
        payload!.DisplayName.Should().Be("S3 Updated");
        payload.ConfigurationSummary.EndpointHost.Should().Be("minio-2.internal:9000");
        payload.ConfigurationSummary.BucketOrContainer.Should().Be("nekohub-updated");
        payload.ConfigurationSummary.Region.Should().Be("ap-southeast-1");
        payload.ConfigurationSummary.ForcePathStyle.Should().BeFalse();

        var overviewResponse = await client.GetAsync("/api/v1/system/storage/providers");
        var overview = await GetResponseDataAsync<StorageProviderOverviewResponse>(overviewResponse);
        overview.Should().NotBeNull();
        overview!.Runtime.ProviderType.Should().Be(StorageProviderTypes.Local);
        overview.Runtime.ProviderName.Should().Be("local");
        overview.Runtime.IsConfigurationDriven.Should().BeTrue();

        var rawJson = await overviewResponse.Content.ReadAsStringAsync();
        rawJson.Should().NotContain("ACCESSKEY2");
        rawJson.Should().NotContain("rotated-secret");
    }

    [Fact]
    public async Task Update_GitHub_Repo_Profile_Should_Succeed_And_Keep_Secret_When_Omitted()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var created = await CreateGitHubRepoProfileAsync(client, "gh-repo-updatable");

        var response = await client.PatchAsJsonAsync($"/api/v1/system/storage/providers/{created.Id}", new
        {
            displayName = "GitHub Repo Updated",
            configuration = new
            {
                owner = "nekohub",
                repo = "assets-repo",
                @ref = "release/v2",
                basePath = "media/optimized",
                visibilityPolicy = "public-only",
                apiBaseUrl = "https://github.example.com/api/v3",
                rawBaseUrl = "https://github.example.com/raw",
                allowDelete = false
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<StorageProviderProfileResponse>(response);
        payload.Should().NotBeNull();
        payload!.DisplayName.Should().Be("GitHub Repo Updated");
        payload.ConfigurationSummary.Reference.Should().Be("release/v2");
        payload.ConfigurationSummary.PathPrefix.Should().Be("media/optimized");
        payload.ConfigurationSummary.VisibilityPolicy.Should().Be("public-only");
        payload.ConfigurationSummary.ApiBaseUrl.Should().Be("https://github.example.com/api/v3");
        payload.ConfigurationSummary.RawBaseUrl.Should().Be("https://github.example.com/raw");
        payload.Capabilities.RequiresAccessProxy.Should().BeTrue();
        payload.Capabilities.SupportsDelete.Should().BeTrue();

        var overviewResponse = await client.GetAsync("/api/v1/system/storage/providers");
        var rawOverview = await overviewResponse.Content.ReadAsStringAsync();
        rawOverview.Should().NotContain("ghp_repo_token_initial");
    }

    [Fact]
    public async Task Set_Default_Should_Ensure_Only_One_Default_And_Update_Runtime_Write_Target()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var local = await CreateLocalProfileAsync(client, "local-one", isDefault: true);
        var s3 = await CreateS3ProfileAsync(client, "s3-two");

        var response = await client.PostAsync($"/api/v1/system/storage/providers/{s3.Id}/set-default", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await GetResponseDataAsync<StorageProviderProfileResponse>(response);
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(s3.Id);
        updated.IsDefault.Should().BeTrue();

        var overview = await GetOverviewAsync(client);
        overview.DefaultProfile.Should().NotBeNull();
        overview.DefaultProfile!.Id.Should().Be(s3.Id);
        overview.Profiles.Single(profile => profile.Id == local.Id).IsDefault.Should().BeFalse();
        overview.Profiles.Single(profile => profile.Id == s3.Id).IsDefault.Should().BeTrue();
        overview.Runtime.ProviderType.Should().Be(StorageProviderTypes.S3Compatible);
        overview.Runtime.ProviderName.Should().Be("s3");
        overview.Runtime.IsConfigurationDriven.Should().BeFalse();
        overview.Runtime.MatchesDefaultProfileType.Should().BeTrue();
        overview.Alignment.RuntimeSelectionSource.Should().Be("database-default-profile");
        overview.Alignment.HasDefaultProfile.Should().BeTrue();
        overview.Alignment.ProviderTypeMatchesDefaultProfile.Should().BeTrue();
        overview.Alignment.Code.Should().Be("runtime_matches_db_default_provider_type");
    }

    [Fact]
    public async Task Delete_Default_Profile_Should_Allow_No_Default_Profile()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var local = await CreateLocalProfileAsync(client, "local-default", isDefault: true);

        var response = await client.DeleteAsync($"/api/v1/system/storage/providers/{local.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<DeleteStorageProviderProfileResponse>(response);
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(local.Id);
        payload.WasDefault.Should().BeTrue();
        payload.Status.Should().Be("deleted");

        var overview = await GetOverviewAsync(client);
        overview.Profiles.Should().BeEmpty();
        overview.DefaultProfile.Should().BeNull();
        overview.Runtime.ProviderType.Should().Be(StorageProviderTypes.Local);
        overview.Runtime.ProviderName.Should().Be("local");
        overview.Alignment.RuntimeSelectionSource.Should().Be("configuration");
        overview.Alignment.HasDefaultProfile.Should().BeFalse();
        overview.Alignment.Code.Should().Be("db_default_profile_missing");
    }

    [Fact]
    public async Task Delete_Profile_With_Bound_Assets_Should_Disable_Profile_And_Keep_Asset_Readable()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var local = await CreateLocalProfileAsync(client, "local-bound", isEnabled: true, isDefault: true);
        var asset = await UploadAssetAsync(client, "bound-profile.png", local.Id);

        var response = await client.DeleteAsync($"/api/v1/system/storage/providers/{local.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<DeleteStorageProviderProfileResponse>(response);
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(local.Id);
        payload.WasDefault.Should().BeTrue();
        payload.Status.Should().Be("disabled");

        var overview = await GetOverviewAsync(client);
        var retainedProfile = overview.Profiles.Single(profile => profile.Id == local.Id);
        retainedProfile.IsEnabled.Should().BeFalse();
        retainedProfile.IsDefault.Should().BeFalse();
        overview.DefaultProfile.Should().BeNull();

        var detailResponse = await client.GetAsync($"/api/v1/assets/{asset.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.StorageProviderProfileId.Should().Be(local.Id);

        using var noRedirectClient = CreateAuthorizedClient(factory, allowAutoRedirect: false);
        var contentResponse = await noRedirectClient.GetAsync($"/api/v1/assets/{asset.Id}/content");
        contentResponse.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);

        var deleteAssetResponse = await client.DeleteAsync($"/api/v1/assets/{asset.Id}");
        deleteAssetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Set_Default_Should_Fail_For_Disabled_Profile()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var local = await CreateLocalProfileAsync(client, "local-disabled", isEnabled: false, isDefault: false);

        var response = await client.PostAsync($"/api/v1/system/storage/providers/{local.Id}/set-default", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_default_requires_enabled");
    }

    private static HttpClient CreateAuthorizedClient(
        NekoHubApiKeyApplicationFactory factory,
        bool allowAutoRedirect = true)
    {
        var client = allowAutoRedirect
            ? factory.CreateClient()
            : factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);
        return client;
    }

    private static async Task<AssetResponse> UploadAssetAsync(
        HttpClient client,
        string fileName,
        Guid storageProviderProfileId)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[128]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        requestContent.Add(fileContent, "File", fileName);
        requestContent.Add(new StringContent(storageProviderProfileId.ToString()), "StorageProviderProfileId");

        var response = await client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        return (await GetResponseDataAsync<AssetResponse>(response))!;
    }

    private static async Task<StorageProviderProfileResponse> CreateLocalProfileAsync(
        HttpClient client,
        string name,
        bool isEnabled = true,
        bool isDefault = false)
    {
        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name,
            providerType = StorageProviderTypes.Local,
            isEnabled,
            isDefault,
            configuration = new
            {
                rootPath = $"storage/{name}",
                createDirectoryIfMissing = true,
                publicBaseUrl = "http://test-server/content"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await GetResponseDataAsync<StorageProviderProfileResponse>(response))!;
    }

    private static async Task<StorageProviderProfileResponse> CreateS3ProfileAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name,
            providerType = StorageProviderTypes.S3Compatible,
            isEnabled = true,
            configuration = new
            {
                providerName = "s3",
                endpoint = "http://minio.internal:9000",
                bucket = $"{name}-bucket",
                region = "us-east-1",
                forcePathStyle = true,
                publicBaseUrl = "https://cdn.example.com/assets"
            },
            secretConfiguration = new
            {
                accessKey = "ACCESSKEY",
                secretKey = "INITIAL_SECRET"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await GetResponseDataAsync<StorageProviderProfileResponse>(response))!;
    }

    private static async Task<StorageProviderProfileResponse> CreateGitHubRepoProfileAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/system/storage/providers", new
        {
            name,
            providerType = StorageProviderTypes.GitHubRepo,
            isEnabled = true,
            configuration = new
            {
                owner = "nekohub",
                repo = "assets-repo",
                @ref = "main",
                basePath = "media/images",
                visibilityPolicy = "private-token",
                apiBaseUrl = "https://api.github.com",
                rawBaseUrl = "https://raw.githubusercontent.com",
                allowDelete = false
            },
            secretConfiguration = new
            {
                token = "ghp_repo_token_initial"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await GetResponseDataAsync<StorageProviderProfileResponse>(response))!;
    }

    private static async Task<StorageProviderOverviewResponse> GetOverviewAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/system/storage/providers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await GetResponseDataAsync<StorageProviderOverviewResponse>(response))!;
    }

    private static async Task<T?> GetResponseDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions);
        return payload != null ? payload.Data : default;
    }

    private static async Task<ApiError?> GetErrorAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
        return payload?.Error;
    }
}

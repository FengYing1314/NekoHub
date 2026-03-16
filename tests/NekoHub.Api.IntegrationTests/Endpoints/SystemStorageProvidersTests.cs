using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Persistence;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class SystemStorageProvidersTests
{
    [Fact]
    public async Task GetProviders_Should_Return_Profiles_Default_And_Runtime_Summary()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        await SeedProfilesAsync(
            factory,
            new StorageProviderProfile(
                id: Guid.CreateVersion7(),
                name: "local-primary",
                providerType: StorageProviderTypes.Local,
                configurationJson: """
                                   {
                                     "rootPath": "storage/assets",
                                     "publicBaseUrl": "http://test-server/content"
                                   }
                                   """,
                capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.Local),
                displayName: "Local Primary",
                isEnabled: true,
                isDefault: true),
            new StorageProviderProfile(
                id: Guid.CreateVersion7(),
                name: "s3-backup",
                providerType: StorageProviderTypes.S3Compatible,
                configurationJson: """
                                   {
                                     "providerName": "s3",
                                     "endpoint": "http://minio.internal:9000",
                                     "bucket": "nekohub",
                                     "region": "us-east-1",
                                     "forcePathStyle": true,
                                     "publicBaseUrl": "https://cdn.example.com/assets",
                                     "accessKey": "AKIA_TEST",
                                     "secretKey": "super-secret"
                                   }
                                   """,
                capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.S3Compatible),
                displayName: "S3 Backup",
                isEnabled: true,
                isDefault: false));

        var response = await client.GetAsync("/api/v1/system/storage/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await ReadPayloadAsync(response);
        payload.Should().NotBeNull();
        payload!.Profiles.Should().HaveCount(2);
        payload.DefaultProfile.Should().NotBeNull();
        payload.DefaultProfile!.Name.Should().Be("local-primary");
        payload.DefaultWriteProfile.Should().NotBeNull();
        payload.DefaultWriteProfile!.Id.Should().Be(payload.DefaultProfile.Id);
        payload.DefaultProfile.ProviderType.Should().Be(StorageProviderTypes.Local);
        payload.DefaultProfile.ConfigurationSummary.RootPath.Should().Be("storage/assets");
        payload.DefaultProfile.ConfigurationSummary.PublicBaseUrl.Should().Be("http://test-server/content");

        var s3Profile = payload.Profiles.Single(profile => profile.ProviderType == StorageProviderTypes.S3Compatible);
        s3Profile.ConfigurationSummary.ProviderName.Should().Be("s3");
        s3Profile.ConfigurationSummary.EndpointHost.Should().Be("minio.internal:9000");
        s3Profile.ConfigurationSummary.BucketOrContainer.Should().Be("nekohub");
        s3Profile.ConfigurationSummary.Region.Should().Be("us-east-1");
        s3Profile.ConfigurationSummary.PublicBaseUrl.Should().Be("https://cdn.example.com/assets");
        s3Profile.ConfigurationSummary.ForcePathStyle.Should().BeTrue();
        s3Profile.Capabilities.SupportsDirectPublicUrl.Should().BeTrue();
        s3Profile.Capabilities.IsPlatformBacked.Should().BeFalse();
        s3Profile.Capabilities.IsExperimental.Should().BeFalse();
        s3Profile.Capabilities.RequiresTokenForPrivateRead.Should().BeFalse();

        payload.Runtime.ProviderName.Should().Be("local");
        payload.Runtime.ProviderType.Should().Be(StorageProviderTypes.Local);
        payload.Runtime.IsConfigurationDriven.Should().BeFalse();
        payload.Runtime.MatchesDefaultProfileType.Should().BeTrue();
        payload.Runtime.Capabilities.RequiresAccessProxy.Should().BeTrue();
        payload.Alignment.RuntimeSelectionSource.Should().Be("database-default-profile");
        payload.Alignment.HasDefaultProfile.Should().BeTrue();
        payload.Alignment.IsDefaultProfileEnabled.Should().BeTrue();
        payload.Alignment.ProviderTypeMatchesDefaultProfile.Should().BeTrue();
        payload.Alignment.Code.Should().Be("runtime_matches_db_default_provider_type");
        payload.Alignment.Message.Should().Contain("database default write profile");

        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().NotContain("AKIA_TEST");
        rawJson.Should().NotContain("super-secret");
        rawJson.Should().NotContain("accessKey");
        rawJson.Should().NotContain("secretKey");
    }

    [Fact]
    public async Task GetProviders_Should_Return_GitHub_Profile_Summary_And_Hide_Token()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        await SeedProfilesAsync(
            factory,
            new StorageProviderProfile(
                id: Guid.CreateVersion7(),
                name: "gh-releases-default",
                providerType: StorageProviderTypes.GitHubReleases,
                configurationJson: """
                                   {
                                     "owner": "nekohub",
                                     "repo": "assets",
                                     "releaseTagMode": "fixed",
                                     "fixedTag": "v1.2.3",
                                     "assetPathPrefix": "images/public",
                                     "visibilityPolicy": "public-first",
                                     "apiBaseUrl": "https://api.github.com",
                                     "rawBaseUrl": "https://raw.githubusercontent.com"
                                   }
                                   """,
                capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.GitHubReleases),
                displayName: "GitHub Releases",
                isEnabled: true,
                isDefault: true,
                secretConfigurationJson: """{"token":"ghp_secret_token"}"""),
            new StorageProviderProfile(
                id: Guid.CreateVersion7(),
                name: "gh-repo-secondary",
                providerType: StorageProviderTypes.GitHubRepo,
                configurationJson: """
                                   {
                                     "owner": "nekohub",
                                     "repo": "assets-repo",
                                     "ref": "main",
                                     "basePath": "media/images",
                                     "visibilityPolicy": "private-token",
                                     "apiBaseUrl": "https://github.example.com/api/v3",
                                     "rawBaseUrl": "https://github.example.com/raw"
                                   }
                                   """,
                capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.GitHubRepo),
                displayName: "GitHub Repo",
                isEnabled: true,
                isDefault: false,
                secretConfigurationJson: """{"token":"ghp_repo_token"}"""));

        var response = await client.GetAsync("/api/v1/system/storage/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await ReadPayloadAsync(response);
        payload.Should().NotBeNull();
        payload!.Profiles.Should().HaveCount(2);

        var releases = payload.Profiles.Single(profile => profile.ProviderType == StorageProviderTypes.GitHubReleases);
        releases.ConfigurationSummary.Owner.Should().Be("nekohub");
        releases.ConfigurationSummary.Repository.Should().Be("assets");
        releases.ConfigurationSummary.ReleaseTagMode.Should().Be("fixed");
        releases.ConfigurationSummary.FixedTag.Should().Be("v1.2.3");
        releases.ConfigurationSummary.PathPrefix.Should().Be("images/public");
        releases.ConfigurationSummary.VisibilityPolicy.Should().Be("public-first");
        releases.ConfigurationSummary.ApiBaseUrl.Should().Be("https://api.github.com");
        releases.ConfigurationSummary.RawBaseUrl.Should().Be("https://raw.githubusercontent.com");
        releases.Capabilities.SupportsPrivateRead.Should().BeFalse();
        releases.Capabilities.SupportsDelete.Should().BeFalse();
        releases.Capabilities.IsPlatformBacked.Should().BeTrue();
        releases.Capabilities.IsExperimental.Should().BeTrue();
        releases.Capabilities.RequiresTokenForPrivateRead.Should().BeFalse();

        var repo = payload.Profiles.Single(profile => profile.ProviderType == StorageProviderTypes.GitHubRepo);
        repo.ConfigurationSummary.Owner.Should().Be("nekohub");
        repo.ConfigurationSummary.Repository.Should().Be("assets-repo");
        repo.ConfigurationSummary.Reference.Should().Be("main");
        repo.ConfigurationSummary.PathPrefix.Should().Be("media/images");
        repo.ConfigurationSummary.VisibilityPolicy.Should().Be("private-token");
        repo.ConfigurationSummary.BasePath.Should().Be("media/images");
        repo.ConfigurationSummary.ApiBaseUrl.Should().Be("https://github.example.com/api/v3");
        repo.ConfigurationSummary.RawBaseUrl.Should().Be("https://github.example.com/raw");
        repo.Capabilities.RequiresAccessProxy.Should().BeTrue();
        repo.Capabilities.SupportsPrivateRead.Should().BeTrue();
        repo.Capabilities.RecommendedForPrimaryStorage.Should().BeFalse();
        repo.Capabilities.IsPlatformBacked.Should().BeTrue();
        repo.Capabilities.IsExperimental.Should().BeTrue();
        repo.Capabilities.RequiresTokenForPrivateRead.Should().BeTrue();

        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().NotContain("ghp_secret_token");
        rawJson.Should().NotContain("ghp_repo_token");
        rawJson.Should().NotContain("\"token\"");
    }

    [Fact]
    public async Task GetProviders_Should_Return_Empty_Profiles_And_Null_Default_When_No_Profiles_Exist()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/v1/system/storage/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await ReadPayloadAsync(response);
        payload.Should().NotBeNull();
        payload!.Profiles.Should().BeEmpty();
        payload.DefaultProfile.Should().BeNull();
        payload.DefaultWriteProfile.Should().BeNull();
        payload.Runtime.ProviderType.Should().Be(StorageProviderTypes.Local);
        payload.Runtime.ProviderName.Should().Be("local");
        payload.Runtime.IsConfigurationDriven.Should().BeTrue();
        payload.Runtime.MatchesDefaultProfileType.Should().BeNull();
        payload.Alignment.RuntimeSelectionSource.Should().Be("configuration");
        payload.Alignment.HasDefaultProfile.Should().BeFalse();
        payload.Alignment.IsDefaultProfileEnabled.Should().BeNull();
        payload.Alignment.ProviderTypeMatchesDefaultProfile.Should().BeNull();
        payload.Alignment.Code.Should().Be("db_default_profile_missing");
        payload.Alignment.Message.Should().Contain("Runtime provider is selected from configuration");
    }

    [Fact]
    public async Task GetProviders_Should_Return_Null_Default_When_No_Profile_Is_Marked_Default()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        await SeedProfilesAsync(
            factory,
            new StorageProviderProfile(
                id: Guid.CreateVersion7(),
                name: "local-secondary",
                providerType: StorageProviderTypes.Local,
                configurationJson: """{"rootPath":"storage/secondary"}""",
                capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.Local),
                isEnabled: true,
                isDefault: false));

        var response = await client.GetAsync("/api/v1/system/storage/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await ReadPayloadAsync(response);
        payload.Should().NotBeNull();
        payload!.Profiles.Should().ContainSingle();
        payload.DefaultProfile.Should().BeNull();
        payload.DefaultWriteProfile.Should().BeNull();
        payload.Runtime.MatchesDefaultProfileType.Should().BeNull();
        payload.Alignment.RuntimeSelectionSource.Should().Be("configuration");
        payload.Alignment.HasDefaultProfile.Should().BeFalse();
        payload.Alignment.IsDefaultProfileEnabled.Should().BeNull();
        payload.Alignment.ProviderTypeMatchesDefaultProfile.Should().BeNull();
        payload.Alignment.Code.Should().Be("db_default_profile_missing");
    }

    [Fact]
    public async Task GetProviders_Should_Use_Database_Default_Profile_As_Runtime_Write_Target()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        await SeedProfilesAsync(
            factory,
            new StorageProviderProfile(
                id: Guid.CreateVersion7(),
                name: "s3-default",
                providerType: StorageProviderTypes.S3Compatible,
                configurationJson: """
                                   {
                                     "providerName": "s3",
                                     "endpoint": "http://minio.internal:9000",
                                     "bucket": "nekohub",
                                     "region": "us-east-1",
                                     "forcePathStyle": true
                                   }
                                   """,
                capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.S3Compatible),
                displayName: "S3 Default",
                isEnabled: true,
                isDefault: true));

        var response = await client.GetAsync("/api/v1/system/storage/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await ReadPayloadAsync(response);
        payload.Should().NotBeNull();
        payload!.DefaultProfile.Should().NotBeNull();
        payload.DefaultWriteProfile.Should().NotBeNull();
        payload.DefaultProfile!.ProviderType.Should().Be(StorageProviderTypes.S3Compatible);
        payload.Runtime.ProviderType.Should().Be(StorageProviderTypes.S3Compatible);
        payload.Runtime.ProviderName.Should().Be("s3");
        payload.Runtime.IsConfigurationDriven.Should().BeFalse();
        payload.Runtime.MatchesDefaultProfileType.Should().BeTrue();
        payload.Alignment.RuntimeSelectionSource.Should().Be("database-default-profile");
        payload.Alignment.HasDefaultProfile.Should().BeTrue();
        payload.Alignment.IsDefaultProfileEnabled.Should().BeTrue();
        payload.Alignment.ProviderTypeMatchesDefaultProfile.Should().BeTrue();
        payload.Alignment.Code.Should().Be("runtime_matches_db_default_provider_type");
        payload.Alignment.Message.Should().Contain("database default write profile");
    }

    [Fact]
    public async Task GetProviders_Should_Return_Disabled_Default_Alignment_When_Default_Profile_Is_Disabled()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        await SeedProfilesAsync(
            factory,
            new StorageProviderProfile(
                id: Guid.CreateVersion7(),
                name: "local-disabled-default",
                providerType: StorageProviderTypes.Local,
                configurationJson: """{"rootPath":"storage/assets"}""",
                capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.Local),
                displayName: "Local Disabled Default",
                isEnabled: false,
                isDefault: true));

        var response = await client.GetAsync("/api/v1/system/storage/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await ReadPayloadAsync(response);
        payload.Should().NotBeNull();
        payload!.DefaultProfile.Should().NotBeNull();
        payload.DefaultWriteProfile.Should().NotBeNull();
        payload.DefaultProfile!.IsEnabled.Should().BeFalse();
        payload.Runtime.ProviderType.Should().Be(StorageProviderTypes.Local);
        payload.Runtime.IsConfigurationDriven.Should().BeFalse();
        payload.Runtime.MatchesDefaultProfileType.Should().BeTrue();
        payload.Alignment.RuntimeSelectionSource.Should().Be("database-default-profile");
        payload.Alignment.HasDefaultProfile.Should().BeTrue();
        payload.Alignment.IsDefaultProfileEnabled.Should().BeFalse();
        payload.Alignment.ProviderTypeMatchesDefaultProfile.Should().BeTrue();
        payload.Alignment.Code.Should().Be("db_default_profile_disabled");
        payload.Alignment.Message.Should().Contain("rejected");
    }

    private static HttpClient CreateAuthorizedClient(NekoHubApiKeyApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);
        return client;
    }

    private static async Task SeedProfilesAsync(
        NekoHubApiKeyApplicationFactory factory,
        params StorageProviderProfile[] profiles)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        dbContext.StorageProviderProfiles.AddRange(profiles);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<StorageProviderOverviewResponse?> ReadPayloadAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<StorageProviderOverviewResponse>>(
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return payload?.Data;
    }
}

using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Persistence;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AssetStorageProviderBindingTests : IntegrationTestBase
{
    public AssetStorageProviderBindingTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Upload_With_Explicit_StorageProviderProfileId_Should_Bind_Profile()
    {
        var profileId = await SeedProfileAsync(
            providerType: StorageProviderTypes.Local,
            isEnabled: true,
            isDefault: false,
            configurationJson: """
                               {
                                 "rootPath": "storage/assets",
                                 "createDirectoryIfMissing": true
                               }
                               """);

        var response = await UploadTestImageAsync(
            fileName: "bind-explicit.png",
            contentType: "image/png",
            content: [1, 2, 3, 4],
            storageProviderProfileId: profileId);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        asset!.StorageProviderProfileId.Should().Be(profileId);

        var detailResponse = await Client.GetAsync($"/api/v1/assets/{asset.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await GetResponseDataAsync<AssetResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.StorageProviderProfileId.Should().Be(profileId);
    }

    [Fact]
    public async Task Upload_Without_Profile_Should_Use_Default_Write_Profile_When_Present()
    {
        var defaultProfileId = await SeedProfileAsync(
            providerType: StorageProviderTypes.Local,
            isEnabled: true,
            isDefault: true,
            configurationJson: """
                               {
                                 "rootPath": "storage/assets",
                                 "createDirectoryIfMissing": true
                               }
                               """);

        var response = await UploadTestImageAsync(
            fileName: "bind-default.png",
            contentType: "image/png",
            content: [5, 6, 7, 8]);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        asset!.StorageProviderProfileId.Should().Be(defaultProfileId);
    }

    [Fact]
    public async Task Upload_With_Nonexistent_Profile_Should_Return_NotFound()
    {
        var response = await UploadTestImageAsync(
            fileName: "bind-missing-profile.png",
            contentType: "image/png",
            content: [9, 9, 9],
            storageProviderProfileId: Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_not_found");
    }

    [Fact]
    public async Task Upload_With_Disabled_Profile_Should_Return_BadRequest()
    {
        var profileId = await SeedProfileAsync(
            providerType: StorageProviderTypes.Local,
            isEnabled: false,
            isDefault: false,
            configurationJson: """
                               {
                                 "rootPath": "storage/assets",
                                 "createDirectoryIfMissing": true
                               }
                               """);

        var response = await UploadTestImageAsync(
            fileName: "bind-disabled-profile.png",
            contentType: "image/png",
            content: [2, 2, 2],
            storageProviderProfileId: profileId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_disabled");
    }

    [Fact]
    public async Task Upload_With_NotWritable_Profile_Should_Return_BadRequest()
    {
        var profileId = await SeedProfileAsync(
            providerType: StorageProviderTypes.GitHubReleases,
            isEnabled: true,
            isDefault: false,
            configurationJson: """
                               {
                                 "owner": "nekohub",
                                 "repo": "assets",
                                 "releaseTagMode": "latest",
                                 "visibilityPolicy": "public-only"
                               }
                               """);

        var response = await UploadTestImageAsync(
            fileName: "bind-readonly-profile.png",
            contentType: "image/png",
            content: [3, 3, 3],
            storageProviderProfileId: profileId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_write_not_supported");
    }

    [Fact]
    public async Task Content_Read_Should_Reject_When_Bound_Profile_Is_Missing()
    {
        var profileId = await SeedProfileAsync(
            providerType: StorageProviderTypes.Local,
            isEnabled: true,
            isDefault: false,
            configurationJson: """
                               {
                                 "rootPath": "storage/assets",
                                 "createDirectoryIfMissing": true
                               }
                               """);

        var uploadResponse = await UploadTestImageAsync(
            fileName: "bind-read-profile.png",
            contentType: "image/png",
            content: [7, 7, 7, 7],
            storageProviderProfileId: profileId);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var uploaded = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        uploaded.Should().NotBeNull();
        uploaded!.StorageProviderProfileId.Should().Be(profileId);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
            var profile = await dbContext.StorageProviderProfiles.FindAsync(profileId);
            profile.Should().NotBeNull();
            dbContext.StorageProviderProfiles.Remove(profile!);
            await dbContext.SaveChangesAsync();
        }

        var contentResponse = await Client.GetAsync($"/api/v1/assets/{uploaded.Id}/content");
        contentResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(contentResponse);
        error.Should().NotBeNull();
        error!.Code.Should().Be("asset_storage_provider_profile_not_found");
    }

    [Fact]
    public async Task Content_Read_Should_Fallback_To_Legacy_Provider_When_Asset_Has_No_Binding()
    {
        var uploadResponse = await UploadTestImageAsync(
            fileName: "legacy-no-binding.png",
            contentType: "image/png",
            content: [4, 4, 4, 4]);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var uploaded = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        uploaded.Should().NotBeNull();
        uploaded!.StorageProviderProfileId.Should().BeNull();

        using var noRedirectClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var contentResponse = await noRedirectClient.GetAsync($"/api/v1/assets/{uploaded.Id}/content");
        contentResponse.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);
        contentResponse.Headers.Location.Should().NotBeNull();
    }

    private async Task<Guid> SeedProfileAsync(
        string providerType,
        bool isEnabled,
        bool isDefault,
        string configurationJson)
    {
        var profile = new StorageProviderProfile(
            id: Guid.CreateVersion7(),
            name: $"{providerType}-{Guid.CreateVersion7():N}",
            providerType: providerType,
            configurationJson: configurationJson,
            capabilities: StorageProviderCapabilityCatalog.GetRequired(providerType),
            isEnabled: isEnabled,
            isDefault: isDefault);

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        dbContext.StorageProviderProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return profile.Id;
    }

    private async Task<HttpResponseMessage> UploadTestImageAsync(
        string fileName,
        string contentType,
        byte[] content,
        Guid? storageProviderProfileId = null)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        requestContent.Add(fileContent, "File", fileName);

        if (storageProviderProfileId.HasValue)
        {
            requestContent.Add(
                new StringContent(storageProviderProfileId.Value.ToString()),
                "StorageProviderProfileId");
        }

        return await Client.PostAsync("/api/v1/assets", requestContent);
    }
}

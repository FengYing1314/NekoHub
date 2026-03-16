using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Persistence;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class SystemGitHubRepoProfileOperationsTests
{
    private const string TestApiKey = "nekohub-test-api-key";

    [Fact]
    public async Task Browse_Should_Succeed_By_GitHubRepo_Profile_Id()
    {
        var stubInvoker = new StubGitHubRepoProfileStorageInvoker
        {
            BrowseHandler = (context, relativePath, recursive, maxDepth, _) =>
            {
                context.VisibilityPolicy.Should().Be("public-only");
                relativePath.Should().Be("images");
                recursive.Should().BeTrue();
                maxDepth.Should().Be(3);

                IReadOnlyList<GitHubRepoDirectoryEntry> entries =
                [
                    new GitHubRepoDirectoryEntry(
                        Name: "cat.png",
                        RelativePath: "images/cat.png",
                        IsDirectory: false,
                        Size: 1024,
                        Sha: "sha-cat",
                        PublicUrl: "https://raw.githubusercontent.com/org/repo/main/assets/images/cat.png"),
                    new GitHubRepoDirectoryEntry(
                        Name: "nested",
                        RelativePath: "images/nested",
                        IsDirectory: true,
                        Size: null,
                        Sha: "sha-dir",
                        PublicUrl: null)
                ];

                return Task.FromResult(entries);
            }
        };

        using var factory = new GitHubRepoInvokerOverrideFactory(stubInvoker);
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: true, visibilityPolicy: "public-only", token: null);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/browse?path=images&recursive=true&maxDepth=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<GitHubRepoBrowseResponse>(response);
        payload.Should().NotBeNull();
        payload!.ProfileId.Should().Be(profileId);
        payload.RequestedPath.Should().Be("images");
        payload.Recursive.Should().BeTrue();
        payload.MaxDepth.Should().Be(3);
        payload.Type.Should().Be("all");
        payload.Keyword.Should().BeNull();
        payload.Total.Should().Be(2);
        payload.Page.Should().Be(1);
        payload.PageSize.Should().Be(50);
        payload.HasMore.Should().BeFalse();
        payload.VisibilityPolicy.Should().Be("public-only");
        payload.UsesControlledRead.Should().BeFalse();
        payload.Items.Should().HaveCount(2);
        payload.Items.Single(item => item.Type == "file").PublicUrl.Should().NotBeNull();
        payload.Items.Single(item => item.Type == "dir").PublicUrl.Should().BeNull();

        stubInvoker.BrowseCallCount.Should().Be(1);
        stubInvoker.LastContext.Should().NotBeNull();
        stubInvoker.LastContext!.Owner.Should().Be("org");
        stubInvoker.LastContext.Repo.Should().Be("repo");
        stubInvoker.LastContext.Ref.Should().Be("main");

        var overviewResponse = await client.GetAsync("/api/v1/system/storage/providers");
        var overviewPayload = await GetResponseDataAsync<StorageProviderOverviewResponse>(overviewResponse);
        overviewPayload.Should().NotBeNull();
        overviewPayload!.Runtime.ProviderType.Should().Be(StorageProviderTypes.Local);
        overviewPayload.Runtime.ProviderName.Should().Be("local");
        overviewPayload.Runtime.IsConfigurationDriven.Should().BeTrue();
    }

    [Fact]
    public async Task Browse_Should_Apply_Filter_Paging_And_Stable_Sort()
    {
        var stubInvoker = new StubGitHubRepoProfileStorageInvoker
        {
            BrowseHandler = (_, _, _, _, _) =>
            {
                IReadOnlyList<GitHubRepoDirectoryEntry> entries =
                [
                    new GitHubRepoDirectoryEntry("zeta.png", "images/zeta.png", false, 20, "sha-zeta", "https://raw/zeta"),
                    new GitHubRepoDirectoryEntry("alpha-dir", "images/alpha-dir", true, null, "sha-dir-a", null),
                    new GitHubRepoDirectoryEntry("alpha.png", "images/alpha.png", false, 10, "sha-alpha", "https://raw/alpha"),
                    new GitHubRepoDirectoryEntry("beta-dir", "images/beta-dir", true, null, "sha-dir-b", null)
                ];

                return Task.FromResult(entries);
            }
        };

        using var factory = new GitHubRepoInvokerOverrideFactory(stubInvoker);
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: true, visibilityPolicy: "public-only", token: null);
        using var client = CreateAuthorizedClient(factory);

        var page1Response = await client.GetAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/browse?path=images&type=file&keyword=a&page=1&pageSize=1");
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await GetResponseDataAsync<GitHubRepoBrowseResponse>(page1Response);
        page1.Should().NotBeNull();
        page1!.Type.Should().Be("file");
        page1.Keyword.Should().Be("a");
        page1.Total.Should().Be(2);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(1);
        page1.HasMore.Should().BeTrue();
        page1.Items.Should().ContainSingle();
        page1.Items[0].Path.Should().Be("images/alpha.png");

        var page2Response = await client.GetAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/browse?path=images&type=file&keyword=a&page=2&pageSize=1");
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await GetResponseDataAsync<GitHubRepoBrowseResponse>(page2Response);
        page2.Should().NotBeNull();
        page2!.Type.Should().Be("file");
        page2.Keyword.Should().Be("a");
        page2.Total.Should().Be(2);
        page2.Page.Should().Be(2);
        page2.PageSize.Should().Be(1);
        page2.HasMore.Should().BeFalse();
        page2.Items.Should().ContainSingle();
        page2.Items[0].Path.Should().Be("images/zeta.png");
    }

    [Fact]
    public async Task Upsert_Should_Succeed_By_GitHubRepo_Profile_Id()
    {
        var stubInvoker = new StubGitHubRepoProfileStorageInvoker
        {
            UpsertHandler = (_, _, request, _) =>
            {
                request.RelativePath.Should().Be("images/new.png");
                request.CommitMessage.Should().Be("chore: add new image");
                request.ExpectedSha.Should().BeNull();

                return Task.FromResult(new GitHubRepoUpsertFileResult(
                    StorageKey: "assets/images/new.png",
                    RelativePath: "images/new.png",
                    Sha: "sha-created",
                    Created: true,
                    PublicUrl: "https://raw.githubusercontent.com/org/repo/main/assets/images/new.png"));
            }
        };

        using var factory = new GitHubRepoInvokerOverrideFactory(stubInvoker);
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: true, visibilityPolicy: "public-only", token: null);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/upsert",
            new
            {
                path = "images/new.png",
                contentBase64 = Convert.ToBase64String([1, 2, 3, 4]),
                commitMessage = "chore: add new image"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<GitHubRepoUpsertResponse>(response);
        payload.Should().NotBeNull();
        payload!.ProfileId.Should().Be(profileId);
        payload.Path.Should().Be("images/new.png");
        payload.Operation.Should().Be("created");
        payload.Size.Should().Be(4);
        payload.Sha.Should().Be("sha-created");
        payload.VisibilityPolicy.Should().Be("public-only");
        payload.UsesControlledRead.Should().BeFalse();
        payload.PublicUrl.Should().NotBeNull();

        stubInvoker.UpsertCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Upsert_With_ExpectedSha_Match_Should_Succeed()
    {
        var stubInvoker = new StubGitHubRepoProfileStorageInvoker
        {
            UpsertHandler = (_, _, request, _) =>
            {
                request.ExpectedSha.Should().Be("sha-current");

                return Task.FromResult(new GitHubRepoUpsertFileResult(
                    StorageKey: "assets/images/new.png",
                    RelativePath: "images/new.png",
                    Sha: "sha-updated",
                    Created: false,
                    PublicUrl: "https://raw.githubusercontent.com/org/repo/main/assets/images/new.png"));
            }
        };

        using var factory = new GitHubRepoInvokerOverrideFactory(stubInvoker);
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: true, visibilityPolicy: "public-only", token: null);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/upsert",
            new
            {
                path = "images/new.png",
                contentBase64 = Convert.ToBase64String([1, 2, 3, 4]),
                expectedSha = "sha-current"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<GitHubRepoUpsertResponse>(response);
        payload.Should().NotBeNull();
        payload!.Operation.Should().Be("updated");
        payload.Sha.Should().Be("sha-updated");
    }

    [Fact]
    public async Task Upsert_With_ExpectedSha_Conflict_Should_Return_Conflict()
    {
        var stubInvoker = new StubGitHubRepoProfileStorageInvoker
        {
            UpsertHandler = (_, _, _, _) => throw new ConflictException(
                "storage_provider_upsert_expected_sha_conflict",
                "expectedSha mismatch.")
        };

        using var factory = new GitHubRepoInvokerOverrideFactory(stubInvoker);
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: true, visibilityPolicy: "public-only", token: null);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/upsert",
            new
            {
                path = "images/new.png",
                contentBase64 = Convert.ToBase64String([1, 2, 3, 4]),
                expectedSha = "sha-old"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_upsert_expected_sha_conflict");
    }

    [Fact]
    public async Task Upsert_With_ExpectedSha_And_Missing_Target_Should_Return_Conflict()
    {
        var stubInvoker = new StubGitHubRepoProfileStorageInvoker
        {
            UpsertHandler = (_, _, _, _) => throw new ConflictException(
                "storage_provider_upsert_expected_sha_target_missing",
                "target missing.")
        };

        using var factory = new GitHubRepoInvokerOverrideFactory(stubInvoker);
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: true, visibilityPolicy: "public-only", token: null);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/upsert",
            new
            {
                path = "images/new.png",
                contentBase64 = Convert.ToBase64String([1, 2, 3, 4]),
                expectedSha = "sha-must-exist"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_upsert_expected_sha_target_missing");
    }

    [Fact]
    public async Task Browse_PrivateProfile_Without_Token_Should_Fail()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: true, visibilityPolicy: "private-token", token: null);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/browse?path=images");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_github_repo_token_required_for_private");
    }

    [Fact]
    public async Task Upsert_PrivateProfile_Should_Not_Expose_PublicUrl()
    {
        var stubInvoker = new StubGitHubRepoProfileStorageInvoker
        {
            UpsertHandler = (_, _, _, _) =>
                Task.FromResult(new GitHubRepoUpsertFileResult(
                    StorageKey: "assets/private/note.txt",
                    RelativePath: "private/note.txt",
                    Sha: "sha-private",
                    Created: false,
                    PublicUrl: null))
        };

        using var factory = new GitHubRepoInvokerOverrideFactory(stubInvoker);
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: true, visibilityPolicy: "private-token", token: "ghp_private_token");
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/upsert",
            new
            {
                path = "private/note.txt",
                contentBase64 = Convert.ToBase64String("private-content"u8.ToArray())
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await GetResponseDataAsync<GitHubRepoUpsertResponse>(response);
        payload.Should().NotBeNull();
        payload!.VisibilityPolicy.Should().Be("private-token");
        payload.UsesControlledRead.Should().BeTrue();
        payload.PublicUrl.Should().BeNull();
    }

    [Fact]
    public async Task Browse_Non_GitHubRepo_Profile_Should_Be_Rejected()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        var profileId = await SeedLocalProfileAsync(factory, isEnabled: true);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/browse?path=images");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_provider_type_mismatch");
    }

    [Fact]
    public async Task Browse_Disabled_Profile_Should_Be_Rejected()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: false, visibilityPolicy: "public-only", token: null);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/browse?path=images");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_profile_disabled");
    }

    [Fact]
    public async Task Browse_Invalid_Path_Should_Be_Rejected()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        var profileId = await SeedGitHubRepoProfileAsync(factory, isEnabled: true, visibilityPolicy: "public-only", token: null);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync(
            $"/api/v1/system/storage/providers/{profileId}/github-repo/browse?path=../secrets");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await GetErrorAsync(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("storage_provider_relative_path_invalid");
    }

    private static HttpClient CreateAuthorizedClient(NekoHubApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestApiKey);
        return client;
    }

    private static async Task<Guid> SeedGitHubRepoProfileAsync(
        NekoHubApplicationFactory factory,
        bool isEnabled,
        string visibilityPolicy,
        string? token)
    {
        var profile = new StorageProviderProfile(
            id: Guid.CreateVersion7(),
            name: $"gh-repo-{Guid.CreateVersion7():N}",
            providerType: StorageProviderTypes.GitHubRepo,
            configurationJson: JsonSerializer.Serialize(new
            {
                owner = "org",
                repo = "repo",
                @ref = "main",
                basePath = "assets",
                visibilityPolicy,
                apiBaseUrl = "https://api.github.com",
                rawBaseUrl = "https://raw.githubusercontent.com",
                allowDelete = false
            }),
            capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.GitHubRepo),
            displayName: "GitHub Repo",
            isEnabled: isEnabled,
            isDefault: false,
            secretConfigurationJson: token is null ? null : JsonSerializer.Serialize(new { token }));

        await SeedProfileAsync(factory, profile);
        return profile.Id;
    }

    private static async Task<Guid> SeedLocalProfileAsync(
        NekoHubApplicationFactory factory,
        bool isEnabled)
    {
        var profile = new StorageProviderProfile(
            id: Guid.CreateVersion7(),
            name: $"local-{Guid.CreateVersion7():N}",
            providerType: StorageProviderTypes.Local,
            configurationJson: JsonSerializer.Serialize(new
            {
                rootPath = "storage/assets",
                createDirectoryIfMissing = true
            }),
            capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.Local),
            displayName: "Local",
            isEnabled: isEnabled,
            isDefault: false);

        await SeedProfileAsync(factory, profile);
        return profile.Id;
    }

    private static async Task SeedProfileAsync(NekoHubApplicationFactory factory, StorageProviderProfile profile)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        dbContext.StorageProviderProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<T?> GetResponseDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
        return payload is null ? default : payload.Data;
    }

    private static async Task<ApiError?> GetErrorAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        return payload?.Error;
    }

    private sealed class GitHubRepoInvokerOverrideFactory(IGitHubRepoProfileStorageInvoker invoker)
        : NekoHubApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGitHubRepoProfileStorageInvoker>();
                services.AddSingleton(invoker);
            });
        }

        protected override IDictionary<string, string?> CreateInMemoryConfiguration()
        {
            var configuration = base.CreateInMemoryConfiguration();
            configuration["Auth:ApiKey:Enabled"] = "true";
            configuration["Auth:ApiKey:Keys:0"] = TestApiKey;
            return configuration;
        }
    }

    private sealed class StubGitHubRepoProfileStorageInvoker : IGitHubRepoProfileStorageInvoker
    {
        public Func<GitHubRepoProfileStorageContext, string?, bool, int, CancellationToken, Task<IReadOnlyList<GitHubRepoDirectoryEntry>>>?
            BrowseHandler { get; init; }

        public Func<GitHubRepoProfileStorageContext, Stream, GitHubRepoUpsertFileRequest, CancellationToken, Task<GitHubRepoUpsertFileResult>>?
            UpsertHandler { get; init; }

        public int BrowseCallCount { get; private set; }

        public int UpsertCallCount { get; private set; }

        public GitHubRepoProfileStorageContext? LastContext { get; private set; }

        public Task<IReadOnlyList<GitHubRepoDirectoryEntry>> BrowseAsync(
            GitHubRepoProfileStorageContext context,
            string? relativePath = null,
            bool recursive = false,
            int maxDepth = 2,
            CancellationToken cancellationToken = default)
        {
            BrowseCallCount++;
            LastContext = context;

            if (BrowseHandler is null)
            {
                throw new InvalidOperationException("BrowseHandler is not configured.");
            }

            return BrowseHandler(context, relativePath, recursive, maxDepth, cancellationToken);
        }

        public Task<GitHubRepoUpsertFileResult> UpsertAsync(
            GitHubRepoProfileStorageContext context,
            Stream content,
            GitHubRepoUpsertFileRequest request,
            CancellationToken cancellationToken = default)
        {
            UpsertCallCount++;
            LastContext = context;

            if (UpsertHandler is null)
            {
                throw new InvalidOperationException("UpsertHandler is not configured.");
            }

            return UpsertHandler(context, content, request, cancellationToken);
        }
    }
}

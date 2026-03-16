using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Infrastructure.Options;
using NekoHub.Infrastructure.Storage.GitHub;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class GitHubRepoAssetStorageTests
{
    [Fact]
    public async Task GetPublicUrlAsync_PublicOnly_Should_Return_Raw_Url()
    {
        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                RawBaseUrl = "https://raw.example.com",
                VisibilityPolicy = "public-only"
            },
            httpClient);

        var publicUrl = await provider.GetPublicUrlAsync("assets/images/cat.png");

        publicUrl.Should().Be("https://raw.example.com/owner/repo/main/assets/images/cat.png");
    }

    [Fact]
    public async Task GetPublicUrlAsync_PrivateToken_Should_Return_Null()
    {
        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "private-token",
                Token = "ghp_private_token"
            },
            httpClient);

        var publicUrl = await provider.GetPublicUrlAsync("assets/images/cat.png");

        publicUrl.Should().BeNull();
    }

    [Fact]
    public async Task OpenReadAsync_PrivateToken_Should_Use_ContentsApi_And_Decode_Base64()
    {
        using var handler = new DelegatingStubHttpMessageHandler((request, _) =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.AbsoluteUri.Should().Contain("/repos/owner/repo/contents/assets/private.txt");
            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().Be("ghp_private_token");

            var payload = new
            {
                type = "file",
                path = "assets/private.txt",
                sha = "abc123",
                encoding = "base64",
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes("hello-private"))
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload)
            });
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "private-token",
                Token = "ghp_private_token"
            },
            httpClient);

        await using var stream = await provider.OpenReadAsync("assets/private.txt");
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        text.Should().Be("hello-private");
    }

    [Fact]
    public async Task UpsertFileAsync_Should_Create_When_NotFound_And_Update_When_Exists()
    {
        var requests = new List<HttpRequestMessage>();
        var putBodies = new List<JsonDocument>();
        var sequence = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new
                {
                    content = new
                    {
                        path = "assets/images/new.png",
                        sha = "sha-created"
                    }
                })
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    type = "file",
                    path = "assets/images/new.png",
                    sha = "sha-existing",
                    encoding = "base64",
                    content = Convert.ToBase64String(Encoding.UTF8.GetBytes("old"))
                })
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    content = new
                    {
                        path = "assets/images/new.png",
                        sha = "sha-updated"
                    }
                })
            }
        });

        using var handler = new DelegatingStubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requests.Add(CloneRequestWithoutBody(request));
            if (request.Method == HttpMethod.Put)
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                putBodies.Add(JsonDocument.Parse(body));
            }

            return sequence.Dequeue();
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only",
                Token = "ghp_token_for_write"
            },
            httpClient);

        await using var createContent = new MemoryStream(Encoding.UTF8.GetBytes("new-content"));
        var created = await provider.UpsertFileAsync(
            createContent,
            new GitHubRepoUpsertFileRequest("images/new.png"));

        created.Created.Should().BeTrue();
        created.Sha.Should().Be("sha-created");
        created.StorageKey.Should().Be("assets/images/new.png");

        await using var updateContent = new MemoryStream(Encoding.UTF8.GetBytes("updated-content"));
        var updated = await provider.UpsertFileAsync(
            updateContent,
            new GitHubRepoUpsertFileRequest("images/new.png"));

        updated.Created.Should().BeFalse();
        updated.Sha.Should().Be("sha-updated");
        updated.StorageKey.Should().Be("assets/images/new.png");

        requests.Should().HaveCount(4);
        requests.Count(message => message.Method == HttpMethod.Put).Should().Be(2);
        requests.Count(message => message.Method == HttpMethod.Get).Should().Be(2);
        putBodies.Should().HaveCount(2);
        putBodies[0].RootElement.GetProperty("message").GetString().Should().Be("chore(nekohub): create images/new.png");
        putBodies[0].RootElement.TryGetProperty("sha", out var createShaNode).Should().BeTrue();
        createShaNode.ValueKind.Should().Be(JsonValueKind.Null);
        putBodies[1].RootElement.GetProperty("message").GetString().Should().Be("chore(nekohub): update images/new.png");
        putBodies[1].RootElement.GetProperty("sha").GetString().Should().Be("sha-existing");

        foreach (var body in putBodies)
        {
            body.Dispose();
        }
    }

    [Fact]
    public async Task UpsertFileAsync_With_ExpectedSha_Match_Should_Update()
    {
        var requests = new List<HttpRequestMessage>();
        var putBodies = new List<JsonDocument>();
        var sequence = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    type = "file",
                    path = "assets/images/item.png",
                    sha = "sha-current",
                    encoding = "base64",
                    content = Convert.ToBase64String(Encoding.UTF8.GetBytes("old"))
                })
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    content = new
                    {
                        path = "assets/images/item.png",
                        sha = "sha-updated"
                    }
                })
            }
        });

        using var handler = new DelegatingStubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requests.Add(CloneRequestWithoutBody(request));
            if (request.Method == HttpMethod.Put)
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                putBodies.Add(JsonDocument.Parse(body));
            }

            return sequence.Dequeue();
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only",
                Token = "ghp_token_for_write"
            },
            httpClient);

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("updated"));
        var updated = await provider.UpsertFileAsync(
            content,
            new GitHubRepoUpsertFileRequest(
                RelativePath: "images/item.png",
                ExpectedSha: "sha-current"));

        updated.Created.Should().BeFalse();
        updated.Sha.Should().Be("sha-updated");
        requests.Count(message => message.Method == HttpMethod.Get).Should().Be(1);
        requests.Count(message => message.Method == HttpMethod.Put).Should().Be(1);
        putBodies.Should().ContainSingle();
        putBodies[0].RootElement.GetProperty("sha").GetString().Should().Be("sha-current");

        foreach (var body in putBodies)
        {
            body.Dispose();
        }
    }

    [Fact]
    public async Task UpsertFileAsync_With_Custom_Commit_Message_Should_Use_Override()
    {
        JsonDocument? putBody = null;
        var sequence = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new
                {
                    content = new
                    {
                        path = "assets/images/custom.png",
                        sha = "sha-custom"
                    }
                })
            }
        });

        using var handler = new DelegatingStubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                putBody = JsonDocument.Parse(body);
            }

            return sequence.Dequeue();
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only",
                Token = "ghp_token_for_write"
            },
            httpClient);

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("custom"));
        await provider.UpsertFileAsync(
            content,
            new GitHubRepoUpsertFileRequest(
                RelativePath: "images/custom.png",
                CommitMessage: "custom upload"));

        putBody.Should().NotBeNull();
        putBody!.RootElement.GetProperty("message").GetString().Should().Be("custom upload");
        putBody.Dispose();
    }

    [Fact]
    public async Task UpsertFileAsync_With_ExpectedSha_Mismatch_Should_Return_Conflict()
    {
        var sequence = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    type = "file",
                    path = "assets/images/item.png",
                    sha = "sha-current",
                    encoding = "base64",
                    content = Convert.ToBase64String(Encoding.UTF8.GetBytes("old"))
                })
            }
        });

        var requestCount = 0;
        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(sequence.Dequeue());
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only",
                Token = "ghp_token_for_write"
            },
            httpClient);

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("updated"));
        Func<Task> action = async () => await provider.UpsertFileAsync(
            content,
            new GitHubRepoUpsertFileRequest(
                RelativePath: "images/item.png",
                ExpectedSha: "sha-outdated"));

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("storage_provider_upsert_expected_sha_conflict");
        requestCount.Should().Be(1);
    }

    [Fact]
    public async Task UpsertFileAsync_With_ExpectedSha_And_Missing_Target_Should_Return_Conflict()
    {
        var requestCount = 0;
        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only",
                Token = "ghp_token_for_write"
            },
            httpClient);

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("created"));
        Func<Task> action = async () => await provider.UpsertFileAsync(
            content,
            new GitHubRepoUpsertFileRequest(
                RelativePath: "images/item.png",
                ExpectedSha: "sha-must-exist"));

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("storage_provider_upsert_expected_sha_target_missing");
        requestCount.Should().Be(1);
    }

    [Fact]
    public async Task ListDirectoryAsync_Should_Normalize_Path_And_Reject_Traversal()
    {
        using var handler = new DelegatingStubHttpMessageHandler((request, _) =>
        {
            request.RequestUri!.AbsoluteUri.Should().Contain("/repos/owner/repo/contents/assets/images");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new object[]
                {
                    new { name = "cat.png", path = "assets/images/cat.png", type = "file", size = 12, sha = "sha-file" },
                    new { name = "sub", path = "assets/images/sub", type = "dir", size = (long?)null, sha = "sha-dir" }
                })
            });
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only"
            },
            httpClient);

        var entries = await provider.ListDirectoryAsync("images", recursive: false);
        entries.Should().HaveCount(2);
        entries.Should().Contain(entry => entry.RelativePath == "images/cat.png" && !entry.IsDirectory);
        entries.Should().Contain(entry => entry.RelativePath == "images/sub" && entry.IsDirectory);

        Func<Task> action = async () => await provider.ListDirectoryAsync("../secrets", recursive: false);
        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Code.Should().Be("storage_provider_relative_path_invalid");
    }

    [Fact]
    public async Task DeleteAsync_Should_Fetch_Sha_And_Send_Delete_Request()
    {
        var requests = new List<HttpRequestMessage>();
        var deleteBodies = new List<JsonDocument>();
        var sequence = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    type = "file",
                    path = "assets/images/cat.png",
                    sha = "sha-delete-target",
                    encoding = "base64",
                    content = Convert.ToBase64String(Encoding.UTF8.GetBytes("cat"))
                })
            },
            new HttpResponseMessage(HttpStatusCode.OK)
        });

        using var handler = new DelegatingStubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requests.Add(CloneRequestWithoutBody(request));
            if (request.Method == HttpMethod.Delete)
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                deleteBodies.Add(JsonDocument.Parse(body));
            }

            return sequence.Dequeue();
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only",
                Token = "ghp_token_for_write"
            },
            httpClient);

        await provider.DeleteAsync(new DeleteStoredAssetRequest("assets/images/cat.png", "remove cat"));

        requests.Count(message => message.Method == HttpMethod.Get).Should().Be(1);
        requests.Count(message => message.Method == HttpMethod.Delete).Should().Be(1);
        deleteBodies.Should().ContainSingle();
        deleteBodies[0].RootElement.GetProperty("sha").GetString().Should().Be("sha-delete-target");
        deleteBodies[0].RootElement.GetProperty("message").GetString().Should().Be("remove cat");
        deleteBodies[0].RootElement.GetProperty("branch").GetString().Should().Be("main");

        foreach (var body in deleteBodies)
        {
            body.Dispose();
        }
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_NotFound_When_Target_Missing()
    {
        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only"
            },
            httpClient);

        Func<Task> action = async () => await provider.DeleteAsync(new DeleteStoredAssetRequest("assets/images/cat.png"));
        var exception = await action.Should().ThrowAsync<NotFoundException>();
        exception.Which.Code.Should().Be("storage_provider_target_not_found");
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_When_Metadata_Has_No_Sha()
    {
        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    type = "file",
                    path = "assets/images/cat.png",
                    encoding = "base64",
                    content = Convert.ToBase64String(Encoding.UTF8.GetBytes("cat"))
                })
            }));
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only",
                Token = "ghp_token_for_write"
            },
            httpClient);

        Func<Task> action = async () => await provider.DeleteAsync(new DeleteStoredAssetRequest("assets/images/cat.png"));
        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Code.Should().Be("storage_provider_delete_result_invalid");
    }

    [Fact]
    public async Task DeleteAsync_Without_Commit_Message_Should_Use_Default_Template()
    {
        JsonDocument? deleteBody = null;
        var sequence = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    type = "file",
                    path = "assets/images/cat.png",
                    sha = "sha-delete-target",
                    encoding = "base64",
                    content = Convert.ToBase64String(Encoding.UTF8.GetBytes("cat"))
                })
            },
            new HttpResponseMessage(HttpStatusCode.OK)
        });

        using var handler = new DelegatingStubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                deleteBody = JsonDocument.Parse(body);
            }

            return sequence.Dequeue();
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = CreateProvider(
            new GitHubRepoStorageOptions
            {
                ProviderName = "github-repo",
                Owner = "owner",
                Repo = "repo",
                Ref = "main",
                BasePath = "assets",
                VisibilityPolicy = "public-only",
                Token = "ghp_token_for_write"
            },
            httpClient);

        await provider.DeleteAsync(new DeleteStoredAssetRequest("assets/images/cat.png"));

        deleteBody.Should().NotBeNull();
        deleteBody!.RootElement.GetProperty("message").GetString().Should().Be("chore(nekohub): delete images/cat.png");
        deleteBody.Dispose();
    }

    private static GitHubRepoAssetStorage CreateProvider(GitHubRepoStorageOptions options, HttpClient httpClient)
    {
        return new GitHubRepoAssetStorage(
            Options.Create(options),
            new StubHttpClientFactory(httpClient));
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com")
        };
    }

    private static HttpRequestMessage CloneRequestWithoutBody(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class DelegatingStubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}

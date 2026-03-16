using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NekoHub.Application.Abstractions.Ai;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Infrastructure.Persistence;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AiProviderProfilesControllerTests
{
    [Fact]
    public async Task Create_List_Update_And_Delete_Should_Mask_ApiKey_And_Persist_Protected_Value()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        const string plainApiKey = "sk-live-secret-123";
        const string systemPrompt = "You are a strict visual model that returns concise factual captions.";

        var createResponse = await client.PostAsJsonAsync("/api/v1/system/ai/providers", new
        {
            name = "openai-primary",
            apiBaseUrl = "https://api.openai-compatible.example/v1",
            apiKey = plainApiKey,
            modelName = "qwen-vl-max",
            defaultSystemPrompt = systemPrompt,
            isActive = true
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await GetResponseDataAsync<AiProviderProfileResponse>(createResponse);
        created.Should().NotBeNull();
        created!.Name.Should().Be("openai-primary");
        created.ApiBaseUrl.Should().Be("https://api.openai-compatible.example/v1");
        created.ApiKey.Should().Be("sk-***");
        created.ModelName.Should().Be("qwen-vl-max");
        created.IsActive.Should().BeTrue();

        var rawCreateJson = await createResponse.Content.ReadAsStringAsync();
        rawCreateJson.Should().NotContain(plainApiKey);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
            var stored = await dbContext.AiProviderProfiles.FindAsync(created.Id);
            stored.Should().NotBeNull();
            stored!.ApiKey.Should().NotBe(plainApiKey);
            stored.ApiKeyMasked.Should().Be("sk-***");
        }

        var listResponse = await client.GetAsync("/api/v1/system/ai/providers");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profiles = await GetResponseDataAsync<List<AiProviderProfileResponse>>(listResponse);
        profiles.Should().NotBeNull();
        var listedProfiles = profiles!;
        listedProfiles.Should().ContainSingle();
        listedProfiles[0].ApiKey.Should().Be("sk-***");

        var activeResponse = await client.GetAsync("/api/v1/system/ai/providers/active");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var active = await GetResponseDataAsync<AiProviderProfileResponse>(activeResponse);
        active.Should().NotBeNull();
        active!.Id.Should().Be(created.Id);
        active.ApiKey.Should().Be("sk-***");

        var updateResponse = await client.PatchAsJsonAsync($"/api/v1/system/ai/providers/{created.Id}", new
        {
            name = "openai-fallback",
            apiBaseUrl = "https://api.gateway.example/v1",
            modelName = "gpt-4o",
            defaultSystemPrompt = "You are a strict multimodal assistant.",
            apiKey = "sk-updated-456"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await GetResponseDataAsync<AiProviderProfileResponse>(updateResponse);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("openai-fallback");
        updated.ApiBaseUrl.Should().Be("https://api.gateway.example/v1");
        updated.ModelName.Should().Be("gpt-4o");
        updated.ApiKey.Should().Be("sk-***");

        var deleteResponse = await client.DeleteAsync($"/api/v1/system/ai/providers/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deleted = await GetResponseDataAsync<DeleteAiProviderProfileResponse>(deleteResponse);
        deleted.Should().NotBeNull();
        deleted!.Id.Should().Be(created.Id);
        deleted.WasActive.Should().BeTrue();
        deleted.Status.Should().Be("deleted");
    }

    [Fact]
    public async Task Create_Second_Active_Profile_Should_Switch_Active_Profile_And_Keep_List_Masked()
    {
        using var factory = new NekoHubApiKeyApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var first = await CreateProfileAsync(client, "provider-a", "sk-provider-a", isActive: true);
        var second = await CreateProfileAsync(client, "provider-b", "key-provider-b", isActive: true);

        var listResponse = await client.GetAsync("/api/v1/system/ai/providers");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profiles = await GetResponseDataAsync<List<AiProviderProfileResponse>>(listResponse);
        profiles.Should().NotBeNull();
        var items = profiles!;
        items.Should().HaveCount(2);
        items.All(profile => profile.ApiKey.EndsWith("***", StringComparison.Ordinal)).Should().BeTrue();
        items.Single(profile => profile.Id == first.Id).IsActive.Should().BeFalse();
        items.Single(profile => profile.Id == second.Id).IsActive.Should().BeTrue();

        var activeResponse = await client.GetAsync("/api/v1/system/ai/providers/active");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var active = await GetResponseDataAsync<AiProviderProfileResponse>(activeResponse);
        active.Should().NotBeNull();
        active!.Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task Test_With_Unsaved_Profile_Values_Should_Call_Ai_Vision_Client_And_Return_Caption()
    {
        using var factory = new AiProviderProfileTestApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/ai/providers/test", new
        {
            apiBaseUrl = "https://api.test-gateway.example/v1",
            apiKey = "sk-test-live",
            modelName = "gpt-4.1-mini",
            defaultSystemPrompt = "Return JSON only."
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await GetResponseDataAsync<AiProviderProfileTestResponse>(response);
        result.Should().NotBeNull();
        result!.Succeeded.Should().BeTrue();
        result.Caption.Should().Be("factory test caption");
        result.ResolvedModelName.Should().Be("gpt-4.1-mini");
        result.ResolvedApiBaseUrl.Should().Be("https://api.test-gateway.example/v1");
        result.ErrorMessage.Should().BeNull();

        factory.VisionClient.LastRequest.Should().NotBeNull();
        factory.VisionClient.LastRequest!.ApiBaseUrl.Should().Be("https://api.test-gateway.example/v1");
        factory.VisionClient.LastRequest.ApiKey.Should().Be("sk-test-live");
        factory.VisionClient.LastRequest.ModelName.Should().Be("gpt-4.1-mini");
        factory.VisionClient.LastRequest.SystemPrompt.Should().Be("Return JSON only.");
        factory.VisionClient.LastRequest.ImageDataUrl.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public async Task Test_With_ProfileId_And_Empty_ApiKey_Should_Reuse_Saved_Secret()
    {
        using var factory = new AiProviderProfileTestApplicationFactory();
        using var client = CreateAuthorizedClient(factory);

        var created = await CreateProfileAsync(client, "provider-for-test", "sk-saved-secret", isActive: true);

        var response = await client.PostAsJsonAsync("/api/v1/system/ai/providers/test", new
        {
            profileId = created.Id
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await GetResponseDataAsync<AiProviderProfileTestResponse>(response);
        result.Should().NotBeNull();
        result!.Succeeded.Should().BeTrue();
        result.ResolvedModelName.Should().Be("gpt-4o");
        result.ResolvedApiBaseUrl.Should().Be("https://api.example.com/v1");

        factory.VisionClient.LastRequest.Should().NotBeNull();
        factory.VisionClient.LastRequest!.ApiKey.Should().Be("sk-saved-secret");
        factory.VisionClient.LastRequest.SystemPrompt.Should().Be("You are a strict multimodal assistant.");
    }

    [Fact]
    public async Task Test_When_Ai_Vision_Client_Fails_Should_Return_Readable_Error_Without_500()
    {
        using var factory = new AiProviderProfileTestApplicationFactory();
        factory.VisionClient.Handler = static (_, _) =>
            throw new AiVisionException("OpenAI vision response JSON did not contain a caption field.");

        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/ai/providers/test", new
        {
            apiBaseUrl = "https://api.test-gateway.example/v1",
            apiKey = "sk-test-live",
            modelName = "gpt-4.1-mini"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await GetResponseDataAsync<AiProviderProfileTestResponse>(response);
        result.Should().NotBeNull();
        result!.Succeeded.Should().BeFalse();
        result.Caption.Should().BeNull();
        result.ErrorMessage.Should().Contain("caption field");
    }

    [Fact]
    public async Task Test_When_Standard_Response_Has_No_Content_Should_Fallback_To_Streaming()
    {
        using var factory = new CompatibleAiVisionApplicationFactory();
        factory.VisionHandler.EnqueueJson(new
        {
            model = "gpt-5.4",
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = (string?)null
                    }
                }
            }
        });
        factory.VisionHandler.EnqueueStreaming(
            """{"model":"gpt-5.4","choices":[{"delta":{"reasoning_content":"Thinking..."}}]}""",
            """{"model":"gpt-5.4","choices":[{"delta":{"content":"{\"caption\":\""}}]}""",
            """{"model":"gpt-5.4","choices":[{"delta":{"content":"Recovered via streaming fallback"}}]}""",
            """{"model":"gpt-5.4","choices":[{"delta":{"content":"\"}"}}]}"""
        );

        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/system/ai/providers/test", new
        {
            apiBaseUrl = "https://api.test-gateway.example/v1",
            apiKey = "sk-test-live",
            modelName = "gpt-5.4"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await GetResponseDataAsync<AiProviderProfileTestResponse>(response);
        result.Should().NotBeNull();
        result!.Succeeded.Should().BeTrue();
        result.Caption.Should().Be("Recovered via streaming fallback");
        result.ResolvedModelName.Should().Be("gpt-5.4");

        factory.VisionHandler.RequestBodies.Should().HaveCount(2);
        using var firstRequest = JsonDocument.Parse(factory.VisionHandler.RequestBodies[0]);
        using var secondRequest = JsonDocument.Parse(factory.VisionHandler.RequestBodies[1]);
        firstRequest.RootElement.TryGetProperty("stream", out _).Should().BeFalse();
        secondRequest.RootElement.GetProperty("stream").GetBoolean().Should().BeTrue();
    }

    private static HttpClient CreateAuthorizedClient(NekoHubApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", NekoHubApiKeyApplicationFactory.TestApiKey);
        return client;
    }

    private static async Task<AiProviderProfileResponse> CreateProfileAsync(
        HttpClient client,
        string name,
        string apiKey,
        bool isActive)
    {
        var response = await client.PostAsJsonAsync("/api/v1/system/ai/providers", new
        {
            name,
            apiBaseUrl = "https://api.example.com/v1",
            apiKey,
            modelName = "gpt-4o",
            defaultSystemPrompt = "You are a strict multimodal assistant.",
            isActive
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await GetResponseDataAsync<AiProviderProfileResponse>(response))!;
    }

    private static async Task<T?> GetResponseDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
        return payload != null ? payload.Data : default;
    }

    private sealed class AiProviderProfileTestApplicationFactory : NekoHubApplicationFactory
    {
        public CapturingAiVisionClient VisionClient { get; } = new();

        protected override IDictionary<string, string?> CreateInMemoryConfiguration()
        {
            var configuration = base.CreateInMemoryConfiguration();
            configuration["Auth:ApiKey:Enabled"] = "true";
            configuration["Auth:ApiKey:Keys:0"] = NekoHubApiKeyApplicationFactory.TestApiKey;
            return configuration;
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiVisionClient>();
                services.AddSingleton<IAiVisionClient>(VisionClient);
            });
        }
    }

    private sealed class CapturingAiVisionClient : IAiVisionClient
    {
        public Func<AiVisionRequest, CancellationToken, Task<AiVisionResponse>> Handler { get; set; } =
            static (request, _) => Task.FromResult(new AiVisionResponse(request.ModelName, "factory test caption"));

        public AiVisionRequest? LastRequest { get; private set; }

        public async Task<AiVisionResponse> GenerateAsync(
            AiVisionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return await Handler(request, cancellationToken);
        }
    }
}

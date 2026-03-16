using FluentAssertions;
using NekoHub.Application.Abstractions.Ai;
using NekoHub.Application.Common.Models;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Security;
using NekoHub.Application.Ai.Commands;
using NekoHub.Application.Ai.Services;
using NekoHub.Domain.Ai;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Unit;

public class AiProviderProfileServiceTests
{
    private const string LegacyRejectedTestImageDataUrl =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jfHsAAAAASUVORK5CYII=";

    [Fact]
    public async Task Create_Should_Mask_ApiKey_And_Expose_Decrypted_Runtime_Profile()
    {
        var repository = new InMemoryAiProviderProfileRepository();
        var service = new AiProviderProfileService(repository, new FakeAiProviderSecretProtector(), new FakeAiVisionClient());

        var created = await service.CreateAsync(new CreateAiProviderProfileCommand(
            Name: "openai-primary",
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-secret-value",
            ModelName: "gpt-4o",
            DefaultSystemPrompt: "You are a strict multimodal assistant.",
            IsActive: true));

        created.ApiKey.Should().Be("sk-***");
        created.IsActive.Should().BeTrue();

        repository.Items.Should().ContainSingle();
        repository.Items[0].ApiKey.Should().Be("protected:sk-secret-value");
        repository.Items[0].ApiKeyMasked.Should().Be("sk-***");

        var runtime = await service.GetActiveRuntimeProfileAsync();
        runtime.Should().NotBeNull();
        runtime!.ApiKey.Should().Be("sk-secret-value");
        runtime.ModelName.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task Activating_New_Profile_Should_Deactivate_Previous_Profile()
    {
        var repository = new InMemoryAiProviderProfileRepository();
        var service = new AiProviderProfileService(repository, new FakeAiProviderSecretProtector(), new FakeAiVisionClient());

        var first = await service.CreateAsync(new CreateAiProviderProfileCommand(
            Name: "provider-a",
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "key-a",
            ModelName: "model-a",
            DefaultSystemPrompt: "Prompt A",
            IsActive: true));

        var second = await service.CreateAsync(new CreateAiProviderProfileCommand(
            Name: "provider-b",
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "key-b",
            ModelName: "model-b",
            DefaultSystemPrompt: "Prompt B",
            IsActive: true));

        repository.Items.Single(item => item.Id == first.Id).IsActive.Should().BeFalse();
        repository.Items.Single(item => item.Id == second.Id).IsActive.Should().BeTrue();

        var active = await service.GetActiveProfileAsync();
        active.Should().NotBeNull();
        active!.Id.Should().Be(second.Id);
        active.ApiKey.Should().Be("key-***");
    }

    [Fact]
    public async Task Create_With_Empty_SystemPrompt_Should_Use_Standard_Default()
    {
        var repository = new InMemoryAiProviderProfileRepository();
        var service = new AiProviderProfileService(repository, new FakeAiProviderSecretProtector(), new FakeAiVisionClient());

        var created = await service.CreateAsync(new CreateAiProviderProfileCommand(
            Name: "openai-default-prompt",
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-secret-value",
            ModelName: "gpt-4o",
            DefaultSystemPrompt: "",
            IsActive: true));

        created.DefaultSystemPrompt.Should().Be(AiProviderProfileService.StandardSystemPrompt);
        repository.Items[0].DefaultSystemPrompt.Should().Be(AiProviderProfileService.StandardSystemPrompt);
    }

    [Fact]
    public async Task Update_With_Empty_SystemPrompt_Should_Reset_To_Standard_Default()
    {
        var repository = new InMemoryAiProviderProfileRepository();
        var service = new AiProviderProfileService(repository, new FakeAiProviderSecretProtector(), new FakeAiVisionClient());

        var created = await service.CreateAsync(new CreateAiProviderProfileCommand(
            Name: "openai-reset-prompt",
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-secret-value",
            ModelName: "gpt-4o",
            DefaultSystemPrompt: "Custom prompt",
            IsActive: true));

        var updated = await service.UpdateAsync(new UpdateAiProviderProfileCommand(
            ProfileId: created.Id,
            Name: OptionalValue<string?>.Unspecified,
            ApiBaseUrl: OptionalValue<string?>.Unspecified,
            ApiKey: OptionalValue<string?>.Unspecified,
            ModelName: OptionalValue<string?>.Unspecified,
            DefaultSystemPrompt: OptionalValue<string?>.From(""),
            IsActive: OptionalValue<bool>.Unspecified));

        updated.DefaultSystemPrompt.Should().Be(AiProviderProfileService.StandardSystemPrompt);
        repository.Items.Single(item => item.Id == created.Id).DefaultSystemPrompt.Should().Be(AiProviderProfileService.StandardSystemPrompt);
    }

    [Fact]
    public async Task Test_Should_Use_Compatible_BuiltIn_Image_Probe()
    {
        var repository = new InMemoryAiProviderProfileRepository();
        var visionClient = new FakeAiVisionClient();
        var service = new AiProviderProfileService(repository, new FakeAiProviderSecretProtector(), visionClient);

        var result = await service.TestAsync(new TestAiProviderProfileCommand(
            ProfileId: null,
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-test-value",
            ModelName: "gpt-4o",
            DefaultSystemPrompt: null));

        result.Succeeded.Should().BeTrue();
        visionClient.LastRequest.Should().NotBeNull();
        visionClient.LastRequest!.ImageDataUrl.Should().StartWith("data:image/png;base64,");
        visionClient.LastRequest.ImageDataUrl.Should().NotBe(LegacyRejectedTestImageDataUrl);
    }

    private sealed class FakeAiProviderSecretProtector : IAiProviderSecretProtector
    {
        public string Protect(string secret) => $"protected:{secret}";

        public string Unprotect(string protectedSecret) => protectedSecret["protected:".Length..];
    }

    private sealed class FakeAiVisionClient : IAiVisionClient
    {
        public AiVisionRequest? LastRequest { get; private set; }

        public Task<AiVisionResponse> GenerateAsync(
            AiVisionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new AiVisionResponse(request.ModelName, "unit test caption"));
        }
    }

    private sealed class InMemoryAiProviderProfileRepository : IAiProviderProfileRepository
    {
        public List<AiProviderProfile> Items { get; } = [];

        public Task AddAsync(AiProviderProfile profile, CancellationToken cancellationToken = default)
        {
            Items.Add(profile);
            return Task.CompletedTask;
        }

        public Task<AiProviderProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.SingleOrDefault(item => item.Id == profileId));
        }

        public Task<AiProviderProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.SingleOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<AiProviderProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items
                .Where(item => item.IsActive)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .FirstOrDefault());
        }

        public Task<bool> ExistsByNameAsync(
            string name,
            Guid? excludeProfileId = null,
            CancellationToken cancellationToken = default)
        {
            var exists = Items.Any(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)
                && (!excludeProfileId.HasValue || item.Id != excludeProfileId.Value));
            return Task.FromResult(exists);
        }

        public Task<IReadOnlyList<AiProviderProfile>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AiProviderProfile>>(Items
                .OrderByDescending(item => item.IsActive)
                .ThenBy(item => item.Name)
                .ToList());
        }

        public Task<IReadOnlyList<AiProviderProfile>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AiProviderProfile>>(Items.Where(item => item.IsActive).ToList());
        }

        public Task DeleteAsync(AiProviderProfile profile, CancellationToken cancellationToken = default)
        {
            Items.Remove(profile);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

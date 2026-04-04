using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Security;
using NekoHub.Application.Ai.Commands;
using NekoHub.Application.Ai.Dtos;
using NekoHub.Application.Ai.Queries;
using NekoHub.Application.Ai.Queries.Dtos;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Ai;

namespace NekoHub.Application.Ai.Services;

public sealed class AiProviderProfileService(
    IAiProviderProfileRepository aiProviderProfileRepository,
    IAiProviderSecretProtector aiProviderSecretProtector) : IAiProviderProfileService
{
    public async Task<IReadOnlyList<AiProviderProfileQueryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await aiProviderProfileRepository.ListAsync(cancellationToken);
        return profiles
            .Select(AiProviderProfileQueryMapper.ToQueryDto)
            .ToList();
    }

    public async Task<AiProviderProfileQueryDto?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        var profile = await aiProviderProfileRepository.GetActiveAsync(cancellationToken);
        return profile is null ? null : AiProviderProfileQueryMapper.ToQueryDto(profile);
    }

    public async Task<AiProviderRuntimeProfileDto?> GetActiveRuntimeProfileAsync(CancellationToken cancellationToken = default)
    {
        var profile = await aiProviderProfileRepository.GetActiveAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        return new AiProviderRuntimeProfileDto(
            Id: profile.Id,
            Name: profile.Name,
            ApiBaseUrl: profile.ApiBaseUrl,
            ApiKey: aiProviderSecretProtector.Unprotect(profile.ApiKey),
            ModelName: profile.ModelName,
            DefaultSystemPrompt: profile.DefaultSystemPrompt);
    }

    public async Task<AiProviderProfileQueryDto> CreateAsync(
        CreateAiProviderProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var name = NormalizeRequired(command.Name, "ai_provider_profile_name_required", "Name is required.");
        var apiBaseUrl = NormalizeAbsoluteUrl(command.ApiBaseUrl, "ai_provider_profile_api_base_url_required");
        var apiKey = NormalizeRequired(command.ApiKey, "ai_provider_profile_api_key_required", "ApiKey is required.");
        var modelName = NormalizeRequired(command.ModelName, "ai_provider_profile_model_name_required", "ModelName is required.");
        var defaultSystemPrompt = NormalizeRequired(
            command.DefaultSystemPrompt,
            "ai_provider_profile_default_system_prompt_required",
            "DefaultSystemPrompt is required.");

        await EnsureUniqueNameAsync(name, null, cancellationToken);

        var isActive = command.IsActive ?? !await HasAnyActiveProfileAsync(cancellationToken);
        var profile = new AiProviderProfile(
            id: Guid.CreateVersion7(),
            name: name,
            apiBaseUrl: apiBaseUrl,
            apiKey: aiProviderSecretProtector.Protect(apiKey),
            apiKeyMasked: MaskApiKey(apiKey),
            modelName: modelName,
            defaultSystemPrompt: defaultSystemPrompt,
            isActive: false);

        if (isActive)
        {
            await ClearActiveProfilesAsync(cancellationToken);
            profile.SetActive(true);
        }

        await aiProviderProfileRepository.AddAsync(profile, cancellationToken);
        await aiProviderProfileRepository.SaveChangesAsync(cancellationToken);

        return AiProviderProfileQueryMapper.ToQueryDto(profile);
    }

    public async Task<AiProviderProfileQueryDto> UpdateAsync(
        UpdateAiProviderProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var profile = await aiProviderProfileRepository.GetByIdAsync(command.ProfileId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "ai_provider_profile_not_found",
                $"AI provider profile '{command.ProfileId}' was not found.");
        }

        var name = command.Name.IsSet
            ? NormalizeRequired(command.Name.Value, "ai_provider_profile_name_required", "Name is required.")
            : profile.Name;
        var apiBaseUrl = command.ApiBaseUrl.IsSet
            ? NormalizeAbsoluteUrl(command.ApiBaseUrl.Value, "ai_provider_profile_api_base_url_required")
            : profile.ApiBaseUrl;
        var modelName = command.ModelName.IsSet
            ? NormalizeRequired(command.ModelName.Value, "ai_provider_profile_model_name_required", "ModelName is required.")
            : profile.ModelName;
        var defaultSystemPrompt = command.DefaultSystemPrompt.IsSet
            ? NormalizeRequired(
                command.DefaultSystemPrompt.Value,
                "ai_provider_profile_default_system_prompt_required",
                "DefaultSystemPrompt is required.")
            : profile.DefaultSystemPrompt;

        await EnsureUniqueNameAsync(name, profile.Id, cancellationToken);

        string? protectedApiKey = null;
        string? maskedApiKey = null;
        if (command.ApiKey.IsSet)
        {
            var apiKey = NormalizeRequired(command.ApiKey.Value, "ai_provider_profile_api_key_required", "ApiKey is required.");
            protectedApiKey = aiProviderSecretProtector.Protect(apiKey);
            maskedApiKey = MaskApiKey(apiKey);
        }

        profile.Rename(name);
        profile.UpdateConnection(apiBaseUrl, protectedApiKey, maskedApiKey, modelName);
        profile.UpdatePrompt(defaultSystemPrompt);

        if (command.IsActive.IsSet)
        {
            if (command.IsActive.Value)
            {
                await ClearActiveProfilesAsync(cancellationToken, profile.Id);
                profile.SetActive(true);
            }
            else
            {
                profile.SetActive(false);
            }
        }

        await aiProviderProfileRepository.SaveChangesAsync(cancellationToken);
        return AiProviderProfileQueryMapper.ToQueryDto(profile);
    }

    public async Task<DeleteAiProviderProfileResultDto> DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await aiProviderProfileRepository.GetByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "ai_provider_profile_not_found",
                $"AI provider profile '{profileId}' was not found.");
        }

        var wasActive = profile.IsActive;
        await aiProviderProfileRepository.DeleteAsync(profile, cancellationToken);
        await aiProviderProfileRepository.SaveChangesAsync(cancellationToken);

        return new DeleteAiProviderProfileResultDto(
            Id: profileId,
            WasActive: wasActive,
            Status: "deleted",
            DeletedAtUtc: DateTimeOffset.UtcNow);
    }

    private async Task EnsureUniqueNameAsync(string name, Guid? excludeProfileId, CancellationToken cancellationToken)
    {
        if (await aiProviderProfileRepository.ExistsByNameAsync(name, excludeProfileId, cancellationToken))
        {
            throw new ValidationException(
                "ai_provider_profile_name_conflict",
                $"AI provider profile name '{name}' already exists.");
        }
    }

    private async Task<bool> HasAnyActiveProfileAsync(CancellationToken cancellationToken)
    {
        var activeProfile = await aiProviderProfileRepository.GetActiveAsync(cancellationToken);
        return activeProfile is not null;
    }

    private async Task ClearActiveProfilesAsync(CancellationToken cancellationToken, Guid? excludeProfileId = null)
    {
        var activeProfiles = await aiProviderProfileRepository.ListActiveAsync(cancellationToken);
        foreach (var activeProfile in activeProfiles.Where(x => x.Id != excludeProfileId))
        {
            activeProfile.SetActive(false);
        }
    }

    private static string NormalizeRequired(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(code, message);
        }

        return value.Trim();
    }

    private static string NormalizeAbsoluteUrl(string? value, string code)
    {
        var normalized = NormalizeRequired(value, code, "ApiBaseUrl is required.");
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            throw new ValidationException(
                "ai_provider_profile_api_base_url_invalid",
                "ApiBaseUrl must be an absolute URL.");
        }

        return normalized.TrimEnd('/');
    }

    private static string MaskApiKey(string apiKey)
    {
        var normalized = apiKey.Trim();
        if (normalized.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
        {
            return "sk-***";
        }

        var prefixLength = Math.Min(4, normalized.Length);
        return $"{normalized[..prefixLength]}***";
    }
}

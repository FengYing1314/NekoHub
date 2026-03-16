using NekoHub.Application.Abstractions.Ai;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Security;
using NekoHub.Application.Ai.Commands;
using NekoHub.Application.Ai.Dtos;
using NekoHub.Application.Ai.Queries;
using NekoHub.Application.Ai.Queries.Dtos;
using NekoHub.Application.Common.Diagnostics;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Ai;

namespace NekoHub.Application.Ai.Services;

public sealed class AiProviderProfileService(
    IAiProviderProfileRepository aiProviderProfileRepository,
    IAiProviderSecretProtector aiProviderSecretProtector,
    IAiVisionClient aiVisionClient) : IAiProviderProfileService
{
    private const int MaxPublicErrorMessageLength = 2048;
    private const string CaptionUserPrompt = "Please analyze this image and return a concise factual caption.";
    // Use a small but normal PNG sample. Some OpenAI-compatible gateways reject the old 1x1 transparent probe image.
    private const string TestImageDataUrl =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAeSURBVChTY7gTEPD/14cT/3HRDNgEkWkGbIJDzgQA1/PFgSlWlfsAAAAASUVORK5CYII=";

    public const string StandardSystemPrompt =
        """你是一个专业的视觉资产分析专家。请仔细分析用户上传的图片，并提取出准确、客观的内容描述 (caption)。你必须且只能返回合法的 JSON 对象，格式为: {"caption": "你的描述文字"}。不要输出任何 Markdown 标记、思考过程或其他无关内容。""";

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
        var defaultSystemPrompt = NormalizeSystemPromptOrDefault(command.DefaultSystemPrompt);

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
            ? NormalizeSystemPromptOrDefault(command.DefaultSystemPrompt.Value)
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

    public async Task<AiProviderProfileTestResultDto> TestAsync(
        TestAiProviderProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var runtimeProfile = await ResolveTestRuntimeProfileAsync(command, cancellationToken);

        try
        {
            var caption = await aiVisionClient.GenerateAsync(
                new AiVisionRequest(
                    ApiBaseUrl: runtimeProfile.ApiBaseUrl,
                    ApiKey: runtimeProfile.ApiKey,
                    ModelName: runtimeProfile.ModelName,
                    SystemPrompt: runtimeProfile.DefaultSystemPrompt,
                    UserPrompt: CaptionUserPrompt,
                    ImageDataUrl: TestImageDataUrl),
                cancellationToken);

            return new AiProviderProfileTestResultDto(
                Succeeded: true,
                Caption: caption.Caption,
                ResolvedModelName: caption.ModelName,
                ResolvedApiBaseUrl: runtimeProfile.ApiBaseUrl,
                ErrorMessage: null);
        }
        catch (AiVisionException exception)
        {
            return new AiProviderProfileTestResultDto(
                Succeeded: false,
                Caption: null,
                ResolvedModelName: runtimeProfile.ModelName,
                ResolvedApiBaseUrl: runtimeProfile.ApiBaseUrl,
                ErrorMessage: PublicErrorMessageSanitizer.Sanitize(
                    exception,
                    MaxPublicErrorMessageLength,
                    "AI provider test failed."));
        }
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

    private async Task<ResolvedTestRuntimeProfile> ResolveTestRuntimeProfileAsync(
        TestAiProviderProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ProfileId.HasValue)
        {
            var profile = await aiProviderProfileRepository.GetByIdAsync(command.ProfileId.Value, cancellationToken);
            if (profile is null)
            {
                throw new NotFoundException(
                    "ai_provider_profile_not_found",
                    $"AI provider profile '{command.ProfileId.Value}' was not found.");
            }

            var resolvedApiKey = string.IsNullOrWhiteSpace(command.ApiKey)
                ? aiProviderSecretProtector.Unprotect(profile.ApiKey)
                : NormalizeRequired(command.ApiKey, "ai_provider_profile_api_key_required", "ApiKey is required.");

            return new ResolvedTestRuntimeProfile(
                ApiBaseUrl: string.IsNullOrWhiteSpace(command.ApiBaseUrl)
                    ? profile.ApiBaseUrl
                    : NormalizeAbsoluteUrl(command.ApiBaseUrl, "ai_provider_profile_api_base_url_required"),
                ApiKey: resolvedApiKey,
                ModelName: string.IsNullOrWhiteSpace(command.ModelName)
                    ? profile.ModelName
                    : NormalizeRequired(command.ModelName, "ai_provider_profile_model_name_required", "ModelName is required."),
                DefaultSystemPrompt: command.DefaultSystemPrompt is null
                    ? profile.DefaultSystemPrompt
                    : NormalizeSystemPromptOrDefault(command.DefaultSystemPrompt));
        }

        return new ResolvedTestRuntimeProfile(
            ApiBaseUrl: NormalizeAbsoluteUrl(command.ApiBaseUrl, "ai_provider_profile_api_base_url_required"),
            ApiKey: NormalizeRequired(command.ApiKey, "ai_provider_profile_api_key_required", "ApiKey is required."),
            ModelName: NormalizeRequired(command.ModelName, "ai_provider_profile_model_name_required", "ModelName is required."),
            DefaultSystemPrompt: NormalizeSystemPromptOrDefault(command.DefaultSystemPrompt));
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

    private static string NormalizeSystemPromptOrDefault(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? StandardSystemPrompt
            : normalized;
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

    private sealed record ResolvedTestRuntimeProfile(
        string ApiBaseUrl,
        string ApiKey,
        string ModelName,
        string DefaultSystemPrompt);
}

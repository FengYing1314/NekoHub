using NekoHub.Application.Ai.Queries.Dtos;
using NekoHub.Domain.Ai;

namespace NekoHub.Application.Ai.Queries;

internal static class AiProviderProfileQueryMapper
{
    public static AiProviderProfileQueryDto ToQueryDto(AiProviderProfile profile)
    {
        return new AiProviderProfileQueryDto(
            Id: profile.Id,
            Name: profile.Name,
            ApiBaseUrl: profile.ApiBaseUrl,
            ApiKey: profile.ApiKeyMasked,
            ModelName: profile.ModelName,
            DefaultSystemPrompt: profile.DefaultSystemPrompt,
            IsActive: profile.IsActive,
            CreatedAtUtc: profile.CreatedAtUtc,
            UpdatedAtUtc: profile.UpdatedAtUtc);
    }
}

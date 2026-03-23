using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Prompts;

internal static class McpPromptInputValidator
{
    public static Guid ParseRequiredAssetId(string? assetId, string promptName)
    {
        if (!Guid.TryParse(assetId, out var parsed))
        {
            throw new ValidationException(
                "prompt_argument_invalid",
                $"Prompt '{promptName}' requires argument 'assetId' as a valid GUID.");
        }

        return parsed;
    }

    public static string ResolveSkillName(string? skillName)
    {
        return string.IsNullOrWhiteSpace(skillName) ? "basic_image_enrich" : skillName.Trim();
    }
}

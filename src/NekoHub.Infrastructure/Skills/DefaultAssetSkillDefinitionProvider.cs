using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;

namespace NekoHub.Infrastructure.Skills;

public sealed class DefaultAssetSkillDefinitionProvider : IAssetSkillDefinitionProvider
{
    private static readonly SkillDefinition BasicImageEnrich = new(
        Name: "basic_image_enrich",
        Description: "Generate thumbnail derivative and basic caption structured result for image assets.",
        Steps:
        [
            new SkillStep("generate_thumbnail"),
            new SkillStep("generate_basic_caption")
        ],
        Order: 100);

    private static readonly IReadOnlyList<SkillDefinition> All = [BasicImageEnrich];

    public IReadOnlyList<SkillDefinition> GetAll()
    {
        return All;
    }

    public IReadOnlyList<SkillDefinition> GetForAssetCreated(AssetCreatedProcessingContext context)
    {
        if (!IsImage(context.ContentType))
        {
            return [];
        }

        return All;
    }

    private static bool IsImage(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
               && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}

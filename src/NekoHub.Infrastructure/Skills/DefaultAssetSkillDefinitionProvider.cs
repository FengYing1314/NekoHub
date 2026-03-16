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

    private static readonly SkillDefinition ExifStrip = new(
        Name: "exif-strip",
        Description: "Remove Exif metadata from the original image asset.",
        Steps:
        [
            new SkillStep("strip_exif_metadata")
        ],
        Order: 300);

    private static readonly SkillDefinition FormatConvert = new(
        Name: "format-convert",
        Description: "Convert the original image asset into a different image format.",
        Steps:
        [
            new SkillStep("convert_image_format")
        ],
        Order: 400);

    private static readonly SkillDefinition Watermark = new(
        Name: "watermark",
        Description: "Draw a text watermark onto the original image asset.",
        Steps:
        [
            new SkillStep("draw_watermark")
        ],
        Order: 500);

    private static readonly IReadOnlyList<SkillDefinition> All = [BasicImageEnrich, ExifStrip, FormatConvert, Watermark];

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

        return [BasicImageEnrich];
    }

    private static bool IsImage(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
               && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}

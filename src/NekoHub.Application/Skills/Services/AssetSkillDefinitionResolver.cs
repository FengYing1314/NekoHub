using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;

namespace NekoHub.Application.Skills.Services;

public sealed class AssetSkillDefinitionResolver(IAssetSkillDefinitionProvider skillDefinitionProvider) : IAssetSkillDefinitionResolver
{
    private static readonly SkillDefinition Thumbnail = new(
        Name: "thumbnail",
        Description: "Generate thumbnail derivative for image assets.",
        Steps:
        [
            new SkillStep("generate_thumbnail")
        ],
        Order: 100);

    private static readonly SkillDefinition AiCaption = new(
        Name: "ai-caption",
        Description: "Generate AI caption structured result for image assets.",
        Steps:
        [
            new SkillStep("generate_basic_caption")
        ],
        Order: 200);

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

    public IReadOnlyList<SkillDefinition> GetDefaultForAssetCreated(AssetCreatedProcessingContext context)
    {
        return skillDefinitionProvider
            .GetForAssetCreated(context)
            .OrderBy(static skill => skill.Order)
            .ThenBy(static skill => skill.Name, StringComparer.Ordinal)
            .ToList();
    }

    public SkillDefinition? ResolveForAsset(AssetCreatedProcessingContext context, string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return null;
        }

        var normalizedSkillId = NormalizeSkillId(skillId);

        if (string.Equals(normalizedSkillId, "thumbnail", StringComparison.Ordinal))
        {
            return IsImage(context.ContentType) ? Thumbnail : null;
        }

        if (string.Equals(normalizedSkillId, "ai-caption", StringComparison.Ordinal))
        {
            return IsImage(context.ContentType) ? AiCaption : null;
        }

        if (string.Equals(normalizedSkillId, "exif-strip", StringComparison.Ordinal))
        {
            return IsImage(context.ContentType) ? ExifStrip : null;
        }

        if (string.Equals(normalizedSkillId, "format-convert", StringComparison.Ordinal))
        {
            return IsImage(context.ContentType) ? FormatConvert : null;
        }

        if (string.Equals(normalizedSkillId, "watermark", StringComparison.Ordinal))
        {
            return IsImage(context.ContentType) ? Watermark : null;
        }

        return skillDefinitionProvider
            .GetAll()
            .FirstOrDefault(definition => string.Equals(definition.Name, normalizedSkillId, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeSkillId(string skillId)
    {
        var normalized = skillId.Trim().ToLowerInvariant();

        return normalized switch
        {
            "generate_thumbnail" => "thumbnail",
            "ai_caption" => "ai-caption",
            "basic_caption" => "ai-caption",
            "generate_basic_caption" => "ai-caption",
            "strip_exif_metadata" => "exif-strip",
            "strip-exif-metadata" => "exif-strip",
            "exif_strip" => "exif-strip",
            "convert_image_format" => "format-convert",
            "convert-image-format" => "format-convert",
            "format_convert" => "format-convert",
            "draw_watermark" => "watermark",
            "draw-watermark" => "watermark",
            _ => normalized
        };
    }

    private static bool IsImage(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
               && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}

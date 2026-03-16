using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Infrastructure.Persistence;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NekoHub.Infrastructure.Processing;

public sealed class WatermarkAssetPostProcessor(
    AssetDbContext dbContext,
    IAssetRepository assetRepository,
    IAssetStorageTargetSelector assetStorageTargetSelector,
    ILogger<WatermarkAssetPostProcessor> logger)
{
    private static readonly string[] PreferredFontFamilies =
    [
        "DejaVu Sans",
        "Liberation Sans",
        "Arial",
        "Segoe UI",
        "Noto Sans",
        "Helvetica",
        "Ubuntu",
        "Cantarell"
    ];

    public async Task ProcessAsync(
        NekoHub.Application.Abstractions.Processing.AssetCreatedProcessingContext context,
        JsonObject? parameters,
        CancellationToken cancellationToken = default)
    {
        if (!IsImage(context.ContentType))
        {
            return;
        }

        var asset = await assetRepository.GetByIdAsync(context.AssetId, cancellationToken);
        if (asset is null)
        {
            return;
        }

        await using var storageLease = await assetStorageTargetSelector.ResolveReadTargetAsync(
            asset.StorageProviderProfileId,
            asset.StorageProvider,
            cancellationToken);

        if (!storageLease.Storage.SupportsWrite)
        {
            throw new InvalidOperationException(
                $"Storage provider '{asset.StorageProvider}' does not support watermark writes.");
        }

        var text = GetStringParameter(parameters, "Text", "NekoHub");
        var opacity = GetFloatParameter(parameters, "Opacity", 0.5f, min: 0f, max: 1f);
        var fontSize = GetIntParameter(parameters, "FontSize", 36, min: 1);
        var position = ParsePosition(GetStringParameter(parameters, "Position", "BottomRight"));

        await using var originalContent = new MemoryStream();
        await using (var sourceStream = await storageLease.Storage.OpenReadAsync(asset.StorageKey, cancellationToken))
        {
            if (sourceStream is null)
            {
                throw new InvalidOperationException(
                    $"Asset content '{asset.StorageKey}' could not be opened for watermarking.");
            }

            await sourceStream.CopyToAsync(originalContent, cancellationToken);
        }

        originalContent.Position = 0;
        var format = await Image.DetectFormatAsync(originalContent, cancellationToken);
        originalContent.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(originalContent, cancellationToken);

        var encoder = ResolveEncoder(format);
        var font = ResolveFont(fontSize);
        var textOptions = CreateTextOptions(font, text, image.Width, image.Height, position);
        var color = Color.White.WithAlpha(opacity);

        image.Mutate(processing => processing.DrawText(textOptions, text, color));

        await using var watermarkedContent = new MemoryStream();
        await image.SaveAsync(watermarkedContent, encoder, cancellationToken);
        watermarkedContent.Position = 0;

        var checksumSha256 = await ComputeSha256Async(watermarkedContent, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            watermarkedContent.Position = 0;
            var stored = await storageLease.Storage.OverwriteAsync(
                watermarkedContent,
                asset.StorageKey,
                BuildOverwriteRequest(asset, watermarkedContent.Length),
                cancellationToken);

            asset.UpdateStoredObjectMetadata(
                size: watermarkedContent.Length,
                checksumSha256: checksumSha256,
                width: image.Width,
                height: image.Height,
                storedFileName: stored.StoredFileName,
                publicUrl: stored.PublicUrl);

            await assetRepository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await TryRestoreOriginalContentAsync(storageLease.Storage, asset, originalContent, cancellationToken);
            throw;
        }
    }

    private async Task TryRestoreOriginalContentAsync(
        IAssetStorage storage,
        Domain.Assets.Asset asset,
        MemoryStream originalContent,
        CancellationToken cancellationToken)
    {
        try
        {
            originalContent.Position = 0;
            await storage.OverwriteAsync(
                originalContent,
                asset.StorageKey,
                BuildOverwriteRequest(asset, originalContent.Length),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to restore original asset content after watermark failure. AssetId={AssetId}, StorageKey={StorageKey}",
                asset.Id,
                asset.StorageKey);
        }
    }

    private Font ResolveFont(int fontSize)
    {
        foreach (var familyName in PreferredFontFamilies)
        {
            if (SystemFonts.TryGet(familyName, out var family))
            {
                return family.CreateFont(fontSize, FontStyle.Bold);
            }
        }

        var fallbackFamily = SystemFonts.Families.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fallbackFamily.Name))
        {
            logger.LogWarning(
                "Preferred watermark fonts are unavailable. Falling back to system font '{FontFamily}'.",
                fallbackFamily.Name);
            return fallbackFamily.CreateFont(fontSize, FontStyle.Bold);
        }

        throw new ValidationException(
            "watermark_font_unavailable",
            "No system font is available for watermark rendering. Install a font such as DejaVu Sans or Arial in the runtime environment.");
    }

    private static RichTextOptions CreateTextOptions(
        Font font,
        string text,
        int imageWidth,
        int imageHeight,
        WatermarkPosition position)
    {
        var measurement = TextMeasurer.MeasureSize(text, new RichTextOptions(font));
        var padding = Math.Max(12f, font.Size / 2f);

        var x = position switch
        {
            WatermarkPosition.TopLeft or WatermarkPosition.BottomLeft => padding,
            WatermarkPosition.TopRight or WatermarkPosition.BottomRight => Math.Max(padding, imageWidth - measurement.Width - padding),
            WatermarkPosition.Center => Math.Max(padding, (imageWidth - measurement.Width) / 2f),
            _ => padding
        };

        var y = position switch
        {
            WatermarkPosition.TopLeft or WatermarkPosition.TopRight => padding,
            WatermarkPosition.BottomLeft or WatermarkPosition.BottomRight => Math.Max(padding, imageHeight - measurement.Height - padding),
            WatermarkPosition.Center => Math.Max(padding, (imageHeight - measurement.Height) / 2f),
            _ => padding
        };

        return new RichTextOptions(font)
        {
            Origin = new PointF(x, y)
        };
    }

    private static WatermarkPosition ParsePosition(string position)
    {
        return position.Trim().ToLowerInvariant() switch
        {
            "bottomright" => WatermarkPosition.BottomRight,
            "bottomleft" => WatermarkPosition.BottomLeft,
            "center" => WatermarkPosition.Center,
            "topright" => WatermarkPosition.TopRight,
            "topleft" => WatermarkPosition.TopLeft,
            _ => throw new ValidationException(
                "watermark_position_invalid",
                $"Position '{position}' is not supported. Supported values: BottomRight, BottomLeft, Center, TopRight, TopLeft.")
        };
    }

    private static StoreAssetRequest BuildOverwriteRequest(Domain.Assets.Asset asset, long fileSize)
    {
        return new StoreAssetRequest(
            FileName: asset.StoredFileName
                      ?? asset.OriginalFileName
                      ?? $"{asset.Id:N}{asset.Extension}",
            ContentType: asset.ContentType,
            Extension: asset.Extension,
            FileSize: fileSize);
    }

    private static IImageEncoder ResolveEncoder(IImageFormat? format)
    {
        if (format is null)
        {
            throw new InvalidOperationException("Unable to determine image format for watermarking.");
        }

        return Configuration.Default.ImageFormatsManager.GetEncoder(format)
               ?? throw new InvalidOperationException(
                   $"No encoder is registered for image format '{format.Name}'.");
    }

    private static string GetStringParameter(JsonObject? parameters, string parameterName, string defaultValue)
    {
        if (TryGetParameter(parameters, parameterName) is JsonValue value
            && value.TryGetValue<string>(out var stringValue)
            && !string.IsNullOrWhiteSpace(stringValue))
        {
            return stringValue.Trim();
        }

        return defaultValue;
    }

    private static float GetFloatParameter(
        JsonObject? parameters,
        string parameterName,
        float defaultValue,
        float min,
        float max)
    {
        var parameter = TryGetParameter(parameters, parameterName);
        if (parameter is null)
        {
            return defaultValue;
        }

        float resolvedValue;
        if (parameter is JsonValue value && value.TryGetValue<float>(out var floatValue))
        {
            resolvedValue = floatValue;
        }
        else if (parameter is JsonValue stringValueNode
                 && stringValueNode.TryGetValue<string>(out var stringValue)
                 && float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            resolvedValue = parsedValue;
        }
        else
        {
            throw new ValidationException(
                "watermark_opacity_invalid",
                $"Parameter '{parameterName}' must be a floating-point value.");
        }

        if (resolvedValue < min || resolvedValue > max)
        {
            throw new ValidationException(
                "watermark_opacity_out_of_range",
                $"Parameter '{parameterName}' must be between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}.");
        }

        return resolvedValue;
    }

    private static int GetIntParameter(JsonObject? parameters, string parameterName, int defaultValue, int min)
    {
        var parameter = TryGetParameter(parameters, parameterName);
        if (parameter is null)
        {
            return defaultValue;
        }

        int resolvedValue;
        if (parameter is JsonValue value && value.TryGetValue<int>(out var intValue))
        {
            resolvedValue = intValue;
        }
        else if (parameter is JsonValue stringValueNode
                 && stringValueNode.TryGetValue<string>(out var stringValue)
                 && int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            resolvedValue = parsedValue;
        }
        else
        {
            throw new ValidationException(
                "watermark_font_size_invalid",
                $"Parameter '{parameterName}' must be an integer value.");
        }

        if (resolvedValue < min)
        {
            throw new ValidationException(
                "watermark_font_size_out_of_range",
                $"Parameter '{parameterName}' must be greater than or equal to {min}.");
        }

        return resolvedValue;
    }

    private static JsonNode? TryGetParameter(JsonObject? parameters, string parameterName)
    {
        if (parameters is null)
        {
            return null;
        }

        foreach (var entry in parameters)
        {
            if (string.Equals(entry.Key, parameterName, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return null;
    }

    private static async Task<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken)
    {
        var originalPosition = content.Position;
        content.Position = 0;

        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(content, cancellationToken);

        content.Position = originalPosition;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsImage(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
               && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private enum WatermarkPosition
    {
        BottomRight,
        BottomLeft,
        Center,
        TopRight,
        TopLeft
    }
}

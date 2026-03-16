using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;

namespace NekoHub.Infrastructure.Processing;

public sealed class FormatConvertAssetPostProcessor(
    AssetDbContext dbContext,
    IAssetRepository assetRepository,
    IAssetStorageTargetSelector assetStorageTargetSelector,
    ILogger<FormatConvertAssetPostProcessor> logger)
{
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
                $"Storage provider '{asset.StorageProvider}' does not support format conversion writes.");
        }

        var target = ResolveTargetFormat(GetRequiredStringParameter(parameters, "TargetFormat"));
        var keepOriginal = GetBoolParameter(parameters, "KeepOriginal", defaultValue: false);

        await using var originalContent = new MemoryStream();
        await using (var sourceStream = await storageLease.Storage.OpenReadAsync(asset.StorageKey, cancellationToken))
        {
            if (sourceStream is null)
            {
                throw new InvalidOperationException(
                    $"Asset content '{asset.StorageKey}' could not be opened for format conversion.");
            }

            await sourceStream.CopyToAsync(originalContent, cancellationToken);
        }

        originalContent.Position = 0;
        using var image = await Image.LoadAsync(originalContent, cancellationToken);

        await using var convertedContent = new MemoryStream();
        await image.SaveAsync(convertedContent, target.Encoder, cancellationToken);
        convertedContent.Position = 0;

        var checksumSha256 = await ComputeSha256Async(convertedContent, cancellationToken);
        convertedContent.Position = 0;

        var oldStorageKey = asset.StorageKey;
        StoredAssetObject? stored = null;

        try
        {
            stored = await storageLease.Storage.StoreAsync(
                convertedContent,
                BuildStoreRequest(asset, target, convertedContent.Length),
                cancellationToken);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                asset.ReplaceStoredObject(
                    contentType: target.ContentType,
                    extension: target.Extension,
                    size: convertedContent.Length,
                    storageProvider: stored.Provider,
                    storageKey: stored.StorageKey,
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
                throw;
            }
        }
        catch
        {
            if (stored is not null)
            {
                await TryDeleteStoredObjectAsync(storageLease.Storage, stored.StorageKey, asset.Id, cancellationToken);
            }

            throw;
        }

        if (!keepOriginal && !string.Equals(oldStorageKey, stored.StorageKey, StringComparison.Ordinal))
        {
            await TryDeleteLegacyObjectAsync(storageLease.Storage, oldStorageKey, asset.Id, cancellationToken);
        }
    }

    private async Task TryDeleteLegacyObjectAsync(
        IAssetStorage storage,
        string storageKey,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(new DeleteStoredAssetRequest(storageKey), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to delete legacy asset object after successful format conversion. AssetId={AssetId}, StorageKey={StorageKey}",
                assetId,
                storageKey);
        }
    }

    private async Task TryDeleteStoredObjectAsync(
        IAssetStorage storage,
        string storageKey,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(new DeleteStoredAssetRequest(storageKey), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to cleanup converted asset object after format conversion failure. AssetId={AssetId}, StorageKey={StorageKey}",
                assetId,
                storageKey);
        }
    }

    private static StoreAssetRequest BuildStoreRequest(
        Domain.Assets.Asset asset,
        FormatConversionTarget target,
        long fileSize)
    {
        var baseFileName = Path.GetFileNameWithoutExtension(
            asset.StoredFileName
            ?? asset.OriginalFileName
            ?? asset.Id.ToString("N"));

        return new StoreAssetRequest(
            FileName: $"{baseFileName}{target.Extension}",
            ContentType: target.ContentType,
            Extension: target.Extension,
            FileSize: fileSize);
    }

    private static FormatConversionTarget ResolveTargetFormat(string targetFormat)
    {
        var normalized = targetFormat.Trim().ToLowerInvariant();

        return normalized switch
        {
            "webp" => new FormatConversionTarget("image/webp", ".webp", new WebpEncoder()),
            "jpg" or "jpeg" => new FormatConversionTarget("image/jpeg", ".jpg", new JpegEncoder()),
            "png" => new FormatConversionTarget("image/png", ".png", new PngEncoder()),
            "gif" => new FormatConversionTarget("image/gif", ".gif", new GifEncoder()),
            "bmp" => new FormatConversionTarget("image/bmp", ".bmp", new BmpEncoder()),
            "tga" => new FormatConversionTarget("image/x-tga", ".tga", new TgaEncoder()),
            "tif" or "tiff" => new FormatConversionTarget("image/tiff", ".tiff", new TiffEncoder()),
            _ => throw new ValidationException(
                "format_convert_target_format_invalid",
                $"TargetFormat '{targetFormat}' is not supported. Supported formats: webp, jpeg, png, gif, bmp, tga, tiff.")
        };
    }

    private static string GetRequiredStringParameter(JsonObject? parameters, string parameterName)
    {
        if (TryGetParameter(parameters, parameterName) is JsonValue value
            && value.TryGetValue<string>(out var stringValue)
            && !string.IsNullOrWhiteSpace(stringValue))
        {
            return stringValue.Trim();
        }

        throw new ValidationException(
            "format_convert_target_format_required",
            $"Parameter '{parameterName}' is required for skill 'format-convert'.");
    }

    private static bool GetBoolParameter(JsonObject? parameters, string parameterName, bool defaultValue)
    {
        var parameter = TryGetParameter(parameters, parameterName);
        if (parameter is null)
        {
            return defaultValue;
        }

        if (parameter is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && bool.TryParse(stringValue, out var parsedBool))
            {
                return parsedBool;
            }
        }

        throw new ValidationException(
            "format_convert_keep_original_invalid",
            $"Parameter '{parameterName}' must be a boolean value for skill 'format-convert'.");
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

    private sealed record FormatConversionTarget(
        string ContentType,
        string Extension,
        IImageEncoder Encoder);
}

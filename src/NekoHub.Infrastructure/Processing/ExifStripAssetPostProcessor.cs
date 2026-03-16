using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Assets.Services;
using NekoHub.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace NekoHub.Infrastructure.Processing;

public sealed class ExifStripAssetPostProcessor(
    AssetDbContext dbContext,
    IAssetRepository assetRepository,
    IAssetStorageTargetSelector assetStorageTargetSelector,
    ILogger<ExifStripAssetPostProcessor> logger) : IAssetPostProcessor
{
    public string Name => "exif-strip";

    public int Order => 300;

    public async Task ProcessAsync(
        AssetCreatedProcessingContext context,
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
                $"Storage provider '{asset.StorageProvider}' does not support overwriting asset content.");
        }

        await using var originalContent = new MemoryStream();
        await using (var sourceStream = await storageLease.Storage.OpenReadAsync(asset.StorageKey, cancellationToken))
        {
            if (sourceStream is null)
            {
                throw new InvalidOperationException(
                    $"Asset content '{asset.StorageKey}' could not be opened for exif stripping.");
            }

            await sourceStream.CopyToAsync(originalContent, cancellationToken);
        }

        originalContent.Position = 0;
        var format = await Image.DetectFormatAsync(originalContent, cancellationToken);
        originalContent.Position = 0;
        using var image = await Image.LoadAsync(originalContent, cancellationToken);

        if (image.Metadata.ExifProfile is null)
        {
            return;
        }

        var encoder = ResolveEncoder(format);
        image.Metadata.ExifProfile = null;

        await using var transformedContent = new MemoryStream();
        await image.SaveAsync(transformedContent, encoder, cancellationToken);
        transformedContent.Position = 0;

        var checksumSha256 = await ComputeSha256Async(transformedContent, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            transformedContent.Position = 0;
            var stored = await storageLease.Storage.OverwriteAsync(
                transformedContent,
                asset.StorageKey,
                BuildOverwriteRequest(asset, transformedContent.Length),
                cancellationToken);

            asset.UpdateStoredObjectMetadata(
                size: transformedContent.Length,
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
                "Failed to restore original asset content after exif-strip failure. AssetId={AssetId}, StorageKey={StorageKey}",
                asset.Id,
                asset.StorageKey);
        }
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
            throw new InvalidOperationException("Unable to determine image format for exif stripping.");
        }

        return Configuration.Default.ImageFormatsManager.GetEncoder(format)
               ?? throw new InvalidOperationException(
                   $"No encoder is registered for image format '{format.Name}'.");
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
}

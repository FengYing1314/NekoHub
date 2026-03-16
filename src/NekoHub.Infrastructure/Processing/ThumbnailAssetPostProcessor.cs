using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Assets.Services;
using NekoHub.Domain.Assets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace NekoHub.Infrastructure.Processing;

public sealed class ThumbnailAssetPostProcessor(
    IAssetRepository assetRepository,
    IAssetStorageTargetSelector assetStorageTargetSelector,
    IAssetDerivativeRepository assetDerivativeRepository,
    ILogger<ThumbnailAssetPostProcessor> logger) : IAssetPostProcessor
{
    private const int ThumbnailMaxSize = 256;

    public string Name => "thumbnail";

    public int Order => 100;

    public async Task ProcessAsync(
        AssetCreatedProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        if (!IsImageContentType(context.ContentType))
        {
            return;
        }

        var existing = await assetDerivativeRepository.GetBySourceAndKindAsync(
            context.AssetId,
            AssetDerivativeKinds.Thumbnail256,
            cancellationToken);
        if (existing is not null)
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

        await using var sourceStream = await storageLease.Storage.OpenReadAsync(asset.StorageKey, cancellationToken);
        if (sourceStream is null)
        {
            return;
        }

        await using var bufferedSourceStream = await CopyToSeekableMemoryStreamAsync(sourceStream, cancellationToken);
        using var image = await Image.LoadAsync(bufferedSourceStream, cancellationToken);
        if (image.Width > ThumbnailMaxSize || image.Height > ThumbnailMaxSize)
        {
            image.Mutate(processing => processing.Resize(new ResizeOptions
            {
                Size = new Size(ThumbnailMaxSize, ThumbnailMaxSize),
                Mode = ResizeMode.Max
            }));
        }

        await using var output = new MemoryStream();
        await image.SaveAsync(output, new PngEncoder(), cancellationToken);
        output.Position = 0;

        var stored = await storageLease.Storage.StoreAsync(
            output,
            new StoreAssetRequest(
                FileName: $"{context.AssetId:N}_thumbnail_{ThumbnailMaxSize}.png",
                ContentType: "image/png",
                Extension: ".png",
                FileSize: output.Length),
            cancellationToken);

        var thumbnail = new AssetDerivative(
            id: Guid.CreateVersion7(),
            sourceAssetId: context.AssetId,
            kind: AssetDerivativeKinds.Thumbnail256,
            contentType: "image/png",
            extension: ".png",
            size: output.Length,
            width: image.Width,
            height: image.Height,
            storageProvider: stored.Provider,
            storageKey: stored.StorageKey,
            publicUrl: stored.PublicUrl);

        await assetDerivativeRepository.AddAsync(thumbnail, cancellationToken);
        try
        {
            await assetDerivativeRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, "IX_AssetDerivatives_SourceAssetId_Kind"))
        {
            try
            {
                await storageLease.Storage.DeleteAsync(
                    new DeleteStoredAssetRequest(stored.StorageKey),
                    cancellationToken);
            }
            catch (Exception cleanupException)
            {
                logger.LogWarning(
                    cleanupException,
                    "Failed to cleanup duplicate thumbnail after concurrent insert conflict. AssetId={AssetId}, StorageKey={StorageKey}",
                    context.AssetId,
                    stored.StorageKey);
            }

            logger.LogInformation(
                "Thumbnail derivative already exists due to concurrent processing. AssetId={AssetId}",
                context.AssetId);
        }
    }

    private static async Task<MemoryStream> CopyToSeekableMemoryStreamAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    private static bool IsImageContentType(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
               && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception, string constraintName)
    {
        return exception.InnerException is PostgresException postgresException
               && string.Equals(postgresException.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
               && string.Equals(postgresException.ConstraintName, constraintName, StringComparison.Ordinal);
    }
}

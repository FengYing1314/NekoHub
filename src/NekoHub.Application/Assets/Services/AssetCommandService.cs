using System.Security.Cryptography;
using NekoHub.Application.Abstractions.Metadata;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Assets.Commands;
using NekoHub.Application.Assets.Dtos;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Assets;

namespace NekoHub.Application.Assets.Services;

public sealed class AssetCommandService(
    IAssetRepository assetRepository,
    IAssetDerivativeRepository assetDerivativeRepository,
    IAssetStorageResolver assetStorageResolver,
    IAssetMetadataExtractor metadataExtractor,
    IAssetProcessingDispatcher assetProcessingDispatcher) : IAssetCommandService
{
    public async Task<AssetDto> UploadAsync(UploadAssetCommand command, CancellationToken cancellationToken = default)
    {
        var originalFileName = NormalizeOriginalFileName(command.OriginalFileName);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ValidationException("asset_filename_invalid", "Original file name is invalid.");
        }

        var uploadStream = await EnsureSeekableStreamAsync(command.Content, cancellationToken);

        ExtractedAssetMetadata metadata;
        try
        {
            uploadStream.Position = 0;
            metadata = await metadataExtractor.ExtractAsync(
                uploadStream,
                originalFileName,
                command.DeclaredContentType,
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new ValidationException("asset_metadata_extract_failed", $"Failed to extract metadata: {exception.Message}");
        }

        var extension = NormalizeExtension(metadata.Extension, originalFileName);
        var contentType = string.IsNullOrWhiteSpace(metadata.ContentType)
            ? (command.DeclaredContentType ?? "application/octet-stream")
            : metadata.ContentType;

        var finalSize = metadata.Size ?? command.DeclaredSize;
        if (finalSize <= 0)
        {
            throw new ValidationException("asset_size_invalid", "Asset size must be greater than zero.");
        }

        // 完整性语义收敛：Checksum 统一由服务端基于上传内容计算 SHA-256，
        // 不依赖客户端输入，也不依赖存储实现，以保证跨 provider 一致性。
        var checksumSha256 = await ComputeSha256Async(uploadStream, cancellationToken);

        uploadStream.Position = 0;
        var storage = assetStorageResolver.ResolveDefault();
        var stored = await storage.StoreAsync(
            uploadStream,
            new StoreAssetRequest(
                FileName: originalFileName,
                ContentType: contentType,
                Extension: extension,
                FileSize: finalSize),
            cancellationToken);

        var asset = new Asset(
            id: Guid.CreateVersion7(),
            type: AssetType.Image,
            originalFileName: originalFileName,
            contentType: contentType,
            extension: extension,
            size: finalSize,
            storageProvider: stored.Provider,
            storageKey: stored.StorageKey,
            storedFileName: stored.StoredFileName,
            width: metadata.Width,
            height: metadata.Height,
            checksumSha256: checksumSha256,
            publicUrl: stored.PublicUrl,
            isPublic: command.IsPublic);

        asset.UpdateAccessibleMetadata(command.Description, command.AltText);
        asset.MarkReady(stored.PublicUrl);

        await assetRepository.AddAsync(asset, cancellationToken);
        await assetRepository.SaveChangesAsync(cancellationToken);
        await assetProcessingDispatcher.DispatchAssetCreatedAsync(
            new AssetCreatedProcessingContext(
                AssetId: asset.Id,
                StorageProvider: asset.StorageProvider,
                StorageKey: asset.StorageKey,
                ContentType: asset.ContentType,
                Extension: asset.Extension,
                Size: asset.Size,
                Width: asset.Width,
                Height: asset.Height,
                ChecksumSha256: asset.ChecksumSha256,
                PublicUrl: asset.PublicUrl,
                CreatedAtUtc: asset.CreatedAtUtc),
            cancellationToken);

        return ToDto(asset);
    }

    public async Task<AssetDto> PatchAsync(PatchAssetMetadataCommand command, CancellationToken cancellationToken = default)
    {
        var asset = await assetRepository.GetByIdAsync(command.AssetId, cancellationToken);
        if (asset is null)
        {
            throw new NotFoundException("asset_not_found", $"Asset '{command.AssetId}' was not found.");
        }

        var originalFileName = asset.OriginalFileName;
        if (command.OriginalFileName.IsSet)
        {
            originalFileName = NormalizePatchedOriginalFileName(command.OriginalFileName.Value);
        }

        asset.UpdateMetadata(
            description: command.Description.IsSet ? command.Description.Value : asset.Description,
            altText: command.AltText.IsSet ? command.AltText.Value : asset.AltText,
            originalFileName: originalFileName);

        if (command.IsPublic.IsSet)
        {
            asset.SetVisibility(command.IsPublic.Value);
        }

        await assetRepository.SaveChangesAsync(cancellationToken);
        return ToDto(asset);
    }

    public async Task<DeleteAssetResultDto> DeleteAsync(DeleteAssetCommand command, CancellationToken cancellationToken = default)
    {
        var asset = await assetRepository.GetByIdAsync(command.AssetId, cancellationToken);
        if (asset is null)
        {
            throw new NotFoundException("asset_not_found", $"Asset '{command.AssetId}' was not found.");
        }

        // 第一版采用硬删除：先删除存储对象，再删除资产记录，保证资源不会残留为“孤儿文件”。
        var storage = assetStorageResolver.Resolve(asset.StorageProvider);
        await storage.DeleteAsync(asset.StorageKey, cancellationToken);

        var derivatives = await assetDerivativeRepository.GetBySourceAssetIdAsync(asset.Id, cancellationToken);
        foreach (var derivative in derivatives)
        {
            var derivativeStorage = assetStorageResolver.Resolve(derivative.StorageProvider);
            await derivativeStorage.DeleteAsync(derivative.StorageKey, cancellationToken);
        }

        await assetDerivativeRepository.DeleteRangeAsync(derivatives, cancellationToken);
        await assetRepository.DeleteAsync(asset, cancellationToken);
        await assetDerivativeRepository.SaveChangesAsync(cancellationToken);

        return new DeleteAssetResultDto(
            Id: command.AssetId,
            Status: "deleted",
            DeletedAtUtc: DateTimeOffset.UtcNow);
    }

    public async Task<BatchDeleteAssetsResultDto> BatchDeleteAsync(
        BatchDeleteAssetsCommand command,
        CancellationToken cancellationToken = default)
    {
        var assetIds = command.AssetIds ?? [];
        var notFoundIds = new List<Guid>();
        var deletedCount = 0;

        foreach (var assetId in assetIds)
        {
            try
            {
                await DeleteAsync(new DeleteAssetCommand(assetId), cancellationToken);
                deletedCount++;
            }
            catch (NotFoundException exception) when (exception.Code == "asset_not_found")
            {
                notFoundIds.Add(assetId);
            }
        }

        return new BatchDeleteAssetsResultDto(
            RequestedCount: assetIds.Count,
            DeletedCount: deletedCount,
            NotFoundIds: notFoundIds);
    }

    private static string NormalizeOriginalFileName(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return string.Empty;
        }

        return Path.GetFileName(originalFileName.Trim());
    }

    private static string? NormalizePatchedOriginalFileName(string? originalFileName)
    {
        if (originalFileName is null)
        {
            return null;
        }

        var normalized = NormalizeOriginalFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException("asset_filename_invalid", "Original file name is invalid.");
        }

        return normalized;
    }

    private static async Task<Stream> EnsureSeekableStreamAsync(Stream source, CancellationToken cancellationToken)
    {
        if (source.CanSeek)
        {
            return source;
        }

        var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return memory;
    }

    private static async Task<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
        {
            throw new InvalidOperationException("Upload stream must be seekable for checksum computation.");
        }

        var originalPosition = content.Position;
        content.Position = 0;

        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(content, cancellationToken);

        content.Position = originalPosition;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeExtension(string? extractedExtension, string originalFileName)
    {
        var fromMetadata = extractedExtension;
        if (string.IsNullOrWhiteSpace(fromMetadata))
        {
            fromMetadata = Path.GetExtension(originalFileName);
        }

        if (string.IsNullOrWhiteSpace(fromMetadata))
        {
            return string.Empty;
        }

        return fromMetadata.StartsWith('.')
            ? fromMetadata.ToLowerInvariant()
            : $".{fromMetadata.ToLowerInvariant()}";
    }

    private static AssetDto ToDto(Asset asset)
    {
        return new AssetDto(
            Id: asset.Id,
            Type: asset.Type,
            Status: asset.Status,
            OriginalFileName: asset.OriginalFileName,
            StoredFileName: asset.StoredFileName,
            ContentType: asset.ContentType,
            Extension: asset.Extension,
            Size: asset.Size,
            Width: asset.Width,
            Height: asset.Height,
            ChecksumSha256: asset.ChecksumSha256,
            StorageProvider: asset.StorageProvider,
            StorageKey: asset.StorageKey,
            PublicUrl: asset.IsPublic ? asset.PublicUrl : null,
            IsPublic: asset.IsPublic,
            Description: asset.Description,
            AltText: asset.AltText,
            CreatedAtUtc: asset.CreatedAtUtc,
            UpdatedAtUtc: asset.UpdatedAtUtc);
    }
}

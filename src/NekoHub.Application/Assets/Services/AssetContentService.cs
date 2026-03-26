using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Assets.Dtos;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Assets;

namespace NekoHub.Application.Assets.Services;

public sealed class AssetContentService(
    IAssetRepository assetRepository,
    IAssetDerivativeRepository assetDerivativeRepository,
    IAssetStorageResolver assetStorageResolver) : IAssetContentService
{
    public async Task<AssetContentRedirectDto> GetRedirectAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var asset = await assetRepository.GetByIdAsync(assetId, cancellationToken);
        if (asset is null || asset.Status is AssetStatus.Deleted || !asset.IsPublic)
        {
            throw AssetNotFound(assetId.ToString());
        }

        var publicUrl = await ResolvePublicUrlAsync(asset, cancellationToken);
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            throw AssetNotFound(assetId.ToString());
        }

        return new AssetContentRedirectDto(
            Id: asset.Id,
            RedirectUrl: publicUrl,
            PreserveMethod: true);
    }

    public async Task<AssetPublicContentStreamDto> OpenPublicContentAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedStorageKey = NormalizeStorageKey(storageKey);
        if (string.IsNullOrWhiteSpace(normalizedStorageKey))
        {
            throw AssetNotFound(storageKey);
        }

        var asset = await assetRepository.GetByStorageKeyAsync(normalizedStorageKey, cancellationToken);
        if (asset is not null)
        {
            EnsurePublicAsset(asset, normalizedStorageKey);
            return await OpenAssetContentAsync(asset, asset.ContentType, cancellationToken);
        }

        var derivative = await assetDerivativeRepository.GetByStorageKeyAsync(normalizedStorageKey, cancellationToken);
        if (derivative is null)
        {
            throw AssetNotFound(normalizedStorageKey);
        }

        var sourceAsset = await assetRepository.GetByIdAsync(derivative.SourceAssetId, cancellationToken);
        if (sourceAsset is null)
        {
            throw AssetNotFound(normalizedStorageKey);
        }

        EnsurePublicAsset(sourceAsset, normalizedStorageKey);
        return await OpenContentAsync(
            storageProvider: derivative.StorageProvider,
            storageKey: derivative.StorageKey,
            contentType: derivative.ContentType,
            notFoundIdentifier: normalizedStorageKey,
            cancellationToken: cancellationToken);
    }

    private async Task<string?> ResolvePublicUrlAsync(Asset asset, CancellationToken cancellationToken)
    {
        var storage = assetStorageResolver.Resolve(asset.StorageProvider);
        var publicUrl = asset.PublicUrl;
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            publicUrl = await storage.GetPublicUrlAsync(asset.StorageKey, cancellationToken);
        }

        await using var contentStream = await storage.OpenReadAsync(asset.StorageKey, cancellationToken);
        return contentStream is null ? null : publicUrl;
    }

    private Task<AssetPublicContentStreamDto> OpenAssetContentAsync(
        Asset asset,
        string contentType,
        CancellationToken cancellationToken)
    {
        return OpenContentAsync(
            storageProvider: asset.StorageProvider,
            storageKey: asset.StorageKey,
            contentType: contentType,
            notFoundIdentifier: asset.Id.ToString(),
            cancellationToken: cancellationToken);
    }

    private async Task<AssetPublicContentStreamDto> OpenContentAsync(
        string storageProvider,
        string storageKey,
        string contentType,
        string notFoundIdentifier,
        CancellationToken cancellationToken)
    {
        var storage = assetStorageResolver.Resolve(storageProvider);
        var contentStream = await storage.OpenReadAsync(storageKey, cancellationToken);
        if (contentStream is null)
        {
            throw AssetNotFound(notFoundIdentifier);
        }

        return new AssetPublicContentStreamDto(contentStream, contentType);
    }

    private static void EnsurePublicAsset(Asset asset, string notFoundIdentifier)
    {
        if (asset.Status is AssetStatus.Deleted || !asset.IsPublic)
        {
            throw AssetNotFound(notFoundIdentifier);
        }
    }

    private static string NormalizeStorageKey(string storageKey)
    {
        return string.IsNullOrWhiteSpace(storageKey)
            ? string.Empty
            : storageKey.Replace('\\', '/').Trim().TrimStart('/');
    }

    private static NotFoundException AssetNotFound(string identifier)
    {
        return new NotFoundException("asset_not_found", $"Asset '{identifier}' was not found.");
    }
}

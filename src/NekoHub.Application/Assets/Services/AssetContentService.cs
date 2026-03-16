using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Assets.Dtos;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Assets;

namespace NekoHub.Application.Assets.Services;

public sealed class AssetContentService(
    IAssetRepository assetRepository,
    IAssetStorageResolver assetStorageResolver) : IAssetContentService
{
    public async Task<AssetContentRedirectDto> GetRedirectAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var asset = await assetRepository.GetByIdAsync(assetId, cancellationToken);
        if (asset is null || asset.Status is AssetStatus.Deleted)
        {
            throw new NotFoundException("asset_not_found", $"Asset '{assetId}' was not found.");
        }

        // 优先使用持久化记录里的 publicUrl；若为空则向存储层请求当前可访问地址。
        var storage = assetStorageResolver.Resolve(asset.StorageProvider);
        var publicUrl = asset.PublicUrl;
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            publicUrl = await storage.GetPublicUrlAsync(asset.StorageKey, cancellationToken);
        }

        await using var contentStream = await storage.OpenReadAsync(asset.StorageKey, cancellationToken);

        // 统一跨存储语义：无论 Local 还是 S3，内容不可访问都收敛为 not found，避免暴露内部存储细节。
        if (string.IsNullOrWhiteSpace(publicUrl) || contentStream is null)
        {
            throw new NotFoundException("asset_not_found", $"Asset '{assetId}' was not found.");
        }

        return new AssetContentRedirectDto(
            Id: asset.Id,
            RedirectUrl: publicUrl,
            PreserveMethod: true);
    }
}

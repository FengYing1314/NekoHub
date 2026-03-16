using NekoHub.Application.Assets.Dtos;

namespace NekoHub.Application.Assets.Services;

public interface IAssetContentService
{
    Task<AssetContentRedirectDto> GetRedirectAsync(Guid assetId, CancellationToken cancellationToken = default);
}

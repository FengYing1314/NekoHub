using NekoHub.Application.Assets.Commands;
using NekoHub.Application.Assets.Dtos;

namespace NekoHub.Application.Assets.Services;

public interface IAssetCommandService
{
    Task<AssetDto> UploadAsync(UploadAssetCommand command, CancellationToken cancellationToken = default);

    Task<AssetDto> PatchAsync(PatchAssetMetadataCommand command, CancellationToken cancellationToken = default);

    Task<BatchDeleteAssetsResultDto> BatchDeleteAsync(BatchDeleteAssetsCommand command, CancellationToken cancellationToken = default);

    Task<DeleteAssetResultDto> DeleteAsync(DeleteAssetCommand command, CancellationToken cancellationToken = default);
}

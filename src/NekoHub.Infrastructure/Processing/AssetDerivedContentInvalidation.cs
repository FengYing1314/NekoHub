using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Domain.Assets;
using NekoHub.Infrastructure.Persistence;

namespace NekoHub.Infrastructure.Processing;

internal static class AssetDerivedContentInvalidation
{
    public static async Task<AssetDerivative?> StageAsync(
        AssetDbContext dbContext,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var thumbnail = await dbContext.AssetDerivatives.SingleOrDefaultAsync(
            derivative => derivative.SourceAssetId == assetId && derivative.Kind == AssetDerivativeKinds.Thumbnail256,
            cancellationToken);
        if (thumbnail is not null)
        {
            dbContext.AssetDerivatives.Remove(thumbnail);
        }

        // 说明结果和预览都依赖源内容；保留用户填写的 Description/AltText。
        var caption = await dbContext.AssetStructuredResults.SingleOrDefaultAsync(
            result => result.SourceAssetId == assetId && result.Kind == AssetStructuredResultKinds.BasicCaption,
            cancellationToken);
        if (caption is not null)
        {
            dbContext.AssetStructuredResults.Remove(caption);
        }

        return thumbnail;
    }
}

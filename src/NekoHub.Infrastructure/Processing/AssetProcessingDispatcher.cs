using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Processing;

namespace NekoHub.Infrastructure.Processing;

public sealed class AssetProcessingDispatcher(
    IEnumerable<IAssetPostProcessor> processors,
    ILogger<AssetProcessingDispatcher> logger) : IAssetProcessingDispatcher
{
    private readonly IReadOnlyList<IAssetPostProcessor> _processors = processors
        .OrderBy(static processor => processor.Order)
        .ThenBy(static processor => processor.Name, StringComparer.Ordinal)
        .ToArray();

    public async Task DispatchAssetCreatedAsync(
        AssetCreatedProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var processor in _processors)
        {
            try
            {
                await processor.ProcessAsync(context, cancellationToken);
            }
            catch (Exception exception)
            {
                // 第一阶段骨架语义：处理步骤失败不影响上传主链路，先记录并继续执行后续步骤。
                logger.LogError(
                    exception,
                    "Asset post-process step failed. Step={StepName}, AssetId={AssetId}",
                    processor.Name,
                    context.AssetId);
            }
        }
    }
}

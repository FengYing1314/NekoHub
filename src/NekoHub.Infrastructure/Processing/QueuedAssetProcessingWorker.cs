using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Processing;

namespace NekoHub.Infrastructure.Processing;

public sealed class QueuedAssetProcessingWorker(
    IAssetProcessingQueue assetProcessingQueue,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<QueuedAssetProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var context in assetProcessingQueue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IAssetProcessingDispatcher>();
                await dispatcher.DispatchAsync(context, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Queued asset processing failed. AssetId={AssetId}",
                    context.Asset.AssetId);
            }
        }
    }
}

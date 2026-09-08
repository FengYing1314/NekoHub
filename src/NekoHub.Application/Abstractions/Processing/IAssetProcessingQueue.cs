namespace NekoHub.Application.Abstractions.Processing;

public interface IAssetProcessingQueue
{
    ValueTask EnqueueAsync(
        AssetProcessingRequest request,
        CancellationToken cancellationToken = default);
}

namespace NekoHub.Application.Abstractions.Processing;

public interface IAssetProcessingDispatcher
{
    Task DispatchAsync(
        AssetProcessingRequest request,
        CancellationToken cancellationToken = default);
}

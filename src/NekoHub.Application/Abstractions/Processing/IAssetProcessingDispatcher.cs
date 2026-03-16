namespace NekoHub.Application.Abstractions.Processing;

public interface IAssetProcessingDispatcher
{
    Task DispatchAssetCreatedAsync(
        AssetCreatedProcessingContext context,
        CancellationToken cancellationToken = default);
}

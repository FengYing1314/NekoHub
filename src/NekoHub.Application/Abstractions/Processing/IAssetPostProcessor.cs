namespace NekoHub.Application.Abstractions.Processing;

public interface IAssetPostProcessor
{
    string Name { get; }

    int Order { get; }

    Task ProcessAsync(AssetCreatedProcessingContext context, CancellationToken cancellationToken = default);
}

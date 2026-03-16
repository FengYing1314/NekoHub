using NekoHub.Application.Abstractions.Processing;

namespace NekoHub.Infrastructure.Processing;

public sealed class NoOpAssetPostProcessor : IAssetPostProcessor
{
    public string Name => "noop";

    public int Order => 0;

    public Task ProcessAsync(
        AssetCreatedProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

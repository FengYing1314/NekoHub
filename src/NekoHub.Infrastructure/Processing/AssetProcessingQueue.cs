using System.Threading.Channels;
using NekoHub.Application.Abstractions.Processing;

namespace NekoHub.Infrastructure.Processing;

public sealed class AssetProcessingQueue : IAssetProcessingQueue
{
    private readonly Channel<AssetProcessingRequest> _channel = Channel.CreateUnbounded<AssetProcessingRequest>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(
        AssetProcessingRequest request,
        CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public IAsyncEnumerable<AssetProcessingRequest> DequeueAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

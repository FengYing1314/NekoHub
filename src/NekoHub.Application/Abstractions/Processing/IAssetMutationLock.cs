namespace NekoHub.Application.Abstractions.Processing;

public interface IAssetMutationLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid assetId, CancellationToken cancellationToken = default);
}

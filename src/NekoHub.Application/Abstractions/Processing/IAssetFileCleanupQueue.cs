namespace NekoHub.Application.Abstractions.Processing;

public sealed record AssetFileCleanupTarget(string StorageProvider, string StorageKey, string? CommitMessage = null);

public interface IAssetFileCleanupQueue
{
    Task<Guid> EnqueueAsync(Guid assetId, Guid? storageProviderProfileId,
        IReadOnlyList<AssetFileCleanupTarget> targets, CancellationToken cancellationToken = default);

    Task TryProcessAsync(Guid jobId, CancellationToken cancellationToken = default);
}

namespace NekoHub.Application.Abstractions.Processing;

public sealed record AssetProcessingJobDto(Guid Id, Guid AssetId, string Status, int Attempts,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string? ErrorMessage);

public interface IAssetProcessingJobService
{
    Task<IReadOnlyList<AssetProcessingJobDto>> ListAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task RetryAsync(Guid assetId, Guid jobId, CancellationToken cancellationToken = default);
}

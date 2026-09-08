using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Diagnostics;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Assets;
using NekoHub.Infrastructure.Persistence;

namespace NekoHub.Infrastructure.Processing;

public sealed class AssetProcessingQueue(
    AssetDbContext dbContext,
    IServiceScopeFactory scopeFactory,
    ILogger<AssetProcessingQueue> logger) : IAssetProcessingQueue, IAssetFileCleanupQueue, IAssetProcessingJobService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private const int PendingLimit = 1000;

    public async ValueTask EnqueueAsync(AssetProcessingRequest request, CancellationToken cancellationToken = default)
    {
        if (await dbContext.AssetProcessingJobs.CountAsync(x => x.Kind == "processing" &&
                (x.Status == "pending" || x.Status == "running"), cancellationToken) >= PendingLimit)
        {
            throw new ConflictException("asset_processing_queue_full", "The processing queue is full. Try again later.");
        }

        dbContext.AssetProcessingJobs.Add(new AssetProcessingJob
        {
            Id = request.JobId ?? Guid.CreateVersion7(),
            AssetId = request.Asset.AssetId,
            PayloadJson = JsonSerializer.Serialize(request)
        });
        // 与同一请求中尚未保存的资产一起提交，返回成功即代表任务已持久化。
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> EnqueueAsync(Guid assetId, Guid? storageProviderProfileId,
        IReadOnlyList<AssetFileCleanupTarget> targets, CancellationToken cancellationToken = default)
    {
        var job = new AssetProcessingJob
        {
            Id = Guid.CreateVersion7(),
            AssetId = assetId,
            StorageProviderProfileId = storageProviderProfileId,
            Kind = "cleanup",
            PayloadJson = JsonSerializer.Serialize(targets)
        };
        dbContext.AssetProcessingJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    public async Task<IReadOnlyList<AssetProcessingJobDto>> ListAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        await RequireAssetAsync(assetId, cancellationToken);
        return await dbContext.AssetProcessingJobs.AsNoTracking()
            .Where(x => x.AssetId == assetId && x.Kind == "processing")
            .OrderByDescending(x => x.CreatedAtUtc).Take(50)
            .Select(x => new AssetProcessingJobDto(x.Id, x.AssetId, x.Status, x.Attempts,
                x.CreatedAtUtc, x.UpdatedAtUtc, x.ErrorMessage)).ToListAsync(cancellationToken);
    }

    public async Task RetryAsync(Guid assetId, Guid jobId, CancellationToken cancellationToken = default)
    {
        await RequireAssetAsync(assetId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var changed = await dbContext.AssetProcessingJobs
            .Where(x => x.Id == jobId && x.AssetId == assetId && x.Kind == "processing" && x.Status == "failed")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "pending")
                .SetProperty(x => x.AvailableAtUtc, now).SetProperty(x => x.UpdatedAtUtc, now)
                .SetProperty(x => x.ErrorMessage, (string?)null), cancellationToken);
        if (changed == 0)
        {
            throw new ConflictException("asset_processing_job_not_retryable", "Only a failed processing job can be retried.");
        }
    }

    public async Task TryProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // 调用方的事务已经提交；清理使用独立上下文，失败不撤销已完成的业务变更。
        try
        {
            using var scope = scopeFactory.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<AssetProcessingQueue>();
            var job = await queue.ClaimAsync(jobId, cancellationToken);
            if (job is not null)
            {
                await queue.RunAsync(job, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Immediate file cleanup deferred. JobId={JobId}", jobId);
        }
    }

    internal async Task<AssetProcessingJob?> ClaimAsync(Guid? cleanupJobId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // 只在领取阶段短暂串行，保证多个实例不会同时领取同一资产的任务。
        await dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1869378921, 1)", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await dbContext.AssetProcessingJobs
            .Where(x => x.Status == "running" && x.LeaseExpiresAtUtc < now && x.Kind == "processing")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "failed")
                .SetProperty(x => x.ErrorMessage, "Processing was interrupted. Check the asset before retrying.")
                .SetProperty(x => x.UpdatedAtUtc, now).SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(x => x.LeaseExpiresAtUtc, (DateTimeOffset?)null), cancellationToken);

        var candidates = dbContext.AssetProcessingJobs.Where(x =>
            (x.Status == "pending" && x.AvailableAtUtc <= now) ||
            (x.Kind == "cleanup" && x.Status == "running" && x.LeaseExpiresAtUtc < now));
        if (cleanupJobId.HasValue)
        {
            candidates = candidates.Where(x => x.Id == cleanupJobId && x.Kind == "cleanup");
        }
        // 指定任务的即时清理也必须等待同一资产的处理结束，避免删除仍在使用的对象。
        candidates = candidates.Where(x => !dbContext.AssetProcessingJobs.Any(other =>
            other.AssetId == x.AssetId && other.Id != x.Id && other.Status == "running" && other.LeaseExpiresAtUtc >= now));

        var job = await candidates.OrderBy(x => x.AvailableAtUtc).ThenBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is not null)
        {
            job.Status = "running";
            job.Attempts++;
            job.LeaseId = Guid.NewGuid();
            job.LeaseExpiresAtUtc = now + LeaseDuration;
            job.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    internal async Task RunAsync(AssetProcessingJob job, CancellationToken stoppingToken)
    {
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = RenewLeaseAsync(job, runCancellation);
        Exception? failure = null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            if (job.Kind == "cleanup")
            {
                var targets = JsonSerializer.Deserialize<List<AssetFileCleanupTarget>>(job.PayloadJson)
                    ?? throw new InvalidOperationException("Invalid cleanup job payload.");
                var selector = scope.ServiceProvider.GetRequiredService<IAssetStorageTargetSelector>();
                foreach (var target in targets)
                {
                    await using var storage = await selector.ResolveReadTargetAsync(job.StorageProviderProfileId,
                        target.StorageProvider, runCancellation.Token);
                    await storage.Storage.DeleteAsync(new DeleteStoredAssetRequest(target.StorageKey, target.CommitMessage), runCancellation.Token);
                }
            }
            else
            {
                var db = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
                var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == job.AssetId, runCancellation.Token);
                if (asset is null)
                {
                    throw new InvalidOperationException("The asset no longer exists.");
                }
                var request = JsonSerializer.Deserialize<AssetProcessingRequest>(job.PayloadJson)
                    ?? throw new InvalidOperationException("Invalid processing job payload.");
                request = request with { Asset = new AssetCreatedProcessingContext(asset.Id, asset.StorageProvider,
                    asset.StorageKey, asset.ContentType, asset.Extension, asset.Size, asset.Width, asset.Height,
                    asset.ChecksumSha256, asset.PublicUrl, asset.CreatedAtUtc), JobId = job.Id };
                await scope.ServiceProvider.GetRequiredService<IAssetProcessingDispatcher>()
                    .DispatchAsync(request, runCancellation.Token);
            }
        }
        catch (Exception exception)
        {
            failure = exception;
            logger.LogError(exception, "Asset job failed. JobId={JobId}, Kind={Kind}", job.Id, job.Kind);
        }
        finally
        {
            await runCancellation.CancelAsync();
            await heartbeat;
        }

        using var completionScope = scopeFactory.CreateScope();
        var completionDb = completionScope.ServiceProvider.GetRequiredService<AssetDbContext>();
        var now = DateTimeOffset.UtcNow;
        var status = failure is null ? "succeeded" : job.Kind == "cleanup" ? "pending" : "failed";
        var error = failure is null ? null : PublicErrorMessageSanitizer.Sanitize(failure, 1000, "Processing failed.");
        var nextAttempt = now.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(job.Attempts, 8))));
        await completionDb.AssetProcessingJobs.Where(x => x.Id == job.Id && x.LeaseId == job.LeaseId && x.Status == "running")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, status)
                .SetProperty(x => x.ErrorMessage, error).SetProperty(x => x.UpdatedAtUtc, now)
                .SetProperty(x => x.AvailableAtUtc, nextAttempt).SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(x => x.LeaseExpiresAtUtc, (DateTimeOffset?)null), CancellationToken.None);
    }

    private async Task RenewLeaseAsync(AssetProcessingJob job, CancellationTokenSource runCancellation)
    {
        try
        {
            while (!runCancellation.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), runCancellation.Token);
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
                var expires = DateTimeOffset.UtcNow + LeaseDuration;
                var changed = await db.AssetProcessingJobs
                    .Where(x => x.Id == job.Id && x.LeaseId == job.LeaseId && x.Status == "running")
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LeaseExpiresAtUtc, expires), runCancellation.Token);
                if (changed == 0)
                {
                    await runCancellation.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not renew job lease. JobId={JobId}", job.Id);
            await runCancellation.CancelAsync();
        }
    }

    private async Task RequireAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Assets.AnyAsync(x => x.Id == assetId, cancellationToken))
        {
            throw new NotFoundException("asset_not_found", $"Asset '{assetId}' was not found.");
        }
    }
}

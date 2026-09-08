using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Common.Models;
using NekoHub.Application.Storage.Commands;
using NekoHub.Application.Storage.Services;
using NekoHub.Domain.Assets;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Persistence;
using NekoHub.Infrastructure.Processing;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class DurableAssetJobsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Enqueue_Should_Commit_Or_Roll_Back_With_The_Unsaved_Asset(bool commit)
    {
        using var factory = new PausedJobsFactory();
        var asset = CreateAsset();
        var request = CreateRequest(asset);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Assets.Add(asset);

            await scope.ServiceProvider.GetRequiredService<AssetProcessingQueue>().EnqueueAsync(request);

            // Enqueue 已执行 SaveChanges，但外层事务提交前，其他请求不能看到资产或任务。
            using var observer = factory.Services.CreateScope();
            var observerDb = observer.ServiceProvider.GetRequiredService<AssetDbContext>();
            (await observerDb.Assets.AnyAsync(x => x.Id == asset.Id)).Should().BeFalse();
            (await observerDb.AssetProcessingJobs.AnyAsync(x => x.AssetId == asset.Id)).Should().BeFalse();
            if (commit)
            {
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
            }
        }

        using var inspection = factory.Services.CreateScope();
        var inspectionDb = inspection.ServiceProvider.GetRequiredService<AssetDbContext>();
        (await inspectionDb.Assets.AnyAsync(x => x.Id == asset.Id)).Should().Be(commit);
        (await inspectionDb.AssetProcessingJobs.AnyAsync(x => x.AssetId == asset.Id)).Should().Be(commit);
    }

    [Fact]
    public async Task Persisted_Job_Should_Be_Claimed_And_Run_From_A_New_Scope()
    {
        using var factory = new PausedJobsFactory();
        var asset = CreateAsset();
        var request = CreateRequest(asset);
        using (var enqueueScope = factory.Services.CreateScope())
        {
            enqueueScope.ServiceProvider.GetRequiredService<AssetDbContext>().Assets.Add(asset);
            await enqueueScope.ServiceProvider.GetRequiredService<AssetProcessingQueue>().EnqueueAsync(request);
        }

        using (var runScope = factory.Services.CreateScope())
        {
            var queue = runScope.ServiceProvider.GetRequiredService<AssetProcessingQueue>();
            var job = await queue.ClaimAsync(null, CancellationToken.None);
            job.Should().NotBeNull();
            job!.Id.Should().Be(request.JobId!.Value);
            await queue.RunAsync(job, CancellationToken.None);
        }

        factory.Dispatcher.Requests.Should().ContainSingle();
        factory.Dispatcher.Requests.Single().Asset.AssetId.Should().Be(asset.Id);
        using var inspection = factory.Services.CreateScope();
        var persisted = await inspection.ServiceProvider.GetRequiredService<AssetDbContext>()
            .AssetProcessingJobs.SingleAsync(x => x.Id == request.JobId);
        persisted.Status.Should().Be("succeeded");
        persisted.Attempts.Should().Be(1);
        persisted.LeaseId.Should().BeNull();
    }

    [Fact]
    public async Task Concurrent_Claims_Should_Reserve_Each_Job_Once_And_Serialize_The_Same_Asset()
    {
        using var factory = new PausedJobsFactory();
        var firstAsset = CreateAsset();
        var secondAsset = CreateAsset();
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AssetDbContext>();
            db.Assets.AddRange(firstAsset, secondAsset);
            var queue = seed.ServiceProvider.GetRequiredService<AssetProcessingQueue>();
            await queue.EnqueueAsync(CreateRequest(firstAsset));
            await queue.EnqueueAsync(CreateRequest(firstAsset));
            await queue.EnqueueAsync(CreateRequest(secondAsset));
        }

        async Task<AssetProcessingJob?> ClaimAsync()
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AssetProcessingQueue>().ClaimAsync(null, CancellationToken.None);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimAsync()));

        var claimed = results.Where(static job => job is not null).Select(static job => job!).ToList();
        claimed.Should().HaveCount(2);
        claimed.Select(static job => job.Id).Should().OnlyHaveUniqueItems();
        claimed.Select(static job => job.AssetId).Should().OnlyHaveUniqueItems();
        using var inspection = factory.Services.CreateScope();
        var dbInspection = inspection.ServiceProvider.GetRequiredService<AssetDbContext>();
        (await dbInspection.AssetProcessingJobs.CountAsync(x => x.Status == "running")).Should().Be(2);
        (await dbInspection.AssetProcessingJobs.CountAsync(x => x.Status == "pending")).Should().Be(1);
    }

    [Fact]
    public async Task Specific_Cleanup_Should_Wait_For_Running_Processing_Of_The_Same_Asset()
    {
        using var factory = new PausedJobsFactory();
        var asset = CreateAsset();
        Guid cleanupId;
        using (var seed = factory.Services.CreateScope())
        {
            seed.ServiceProvider.GetRequiredService<AssetDbContext>().Assets.Add(asset);
            var queue = seed.ServiceProvider.GetRequiredService<AssetProcessingQueue>();
            await queue.EnqueueAsync(CreateRequest(asset));
            cleanupId = await queue.EnqueueAsync(asset.Id, null, [new("local", "obsolete.png")]);
        }

        using var processingScope = factory.Services.CreateScope();
        var processingQueue = processingScope.ServiceProvider.GetRequiredService<AssetProcessingQueue>();
        var processingJob = await processingQueue.ClaimAsync(null, CancellationToken.None);
        processingJob!.Kind.Should().Be("processing");
        using var cleanupScope = factory.Services.CreateScope();
        var cleanupQueue = cleanupScope.ServiceProvider.GetRequiredService<AssetProcessingQueue>();

        (await cleanupQueue.ClaimAsync(cleanupId, CancellationToken.None)).Should().BeNull();

        await processingQueue.RunAsync(processingJob, CancellationToken.None);
        (await cleanupQueue.ClaimAsync(cleanupId, CancellationToken.None))!.Id.Should().Be(cleanupId);
    }

    [Fact]
    public async Task Expired_Processing_Should_Fail_Until_Explicit_Retry()
    {
        using var factory = new PausedJobsFactory();
        var asset = CreateAsset();
        var job = CreateExpiredJob(asset.Id, "processing");
        job.PayloadJson = JsonSerializer.Serialize(CreateRequest(asset));
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AssetDbContext>();
            db.Assets.Add(asset);
            db.AssetProcessingJobs.Add(job);
            await db.SaveChangesAsync();
        }

        using (var claim = factory.Services.CreateScope())
        {
            var queue = claim.ServiceProvider.GetRequiredService<AssetProcessingQueue>();
            (await queue.ClaimAsync(null, CancellationToken.None)).Should().BeNull();
            var failed = (await queue.ListAsync(asset.Id)).Single();
            failed.Status.Should().Be("failed");
            failed.ErrorMessage.Should().Contain("interrupted");
            await queue.RetryAsync(asset.Id, job.Id);
        }

        using var retry = factory.Services.CreateScope();
        var retried = await retry.ServiceProvider.GetRequiredService<AssetProcessingQueue>().ClaimAsync(null, CancellationToken.None);
        retried!.Id.Should().Be(job.Id);
        retried.Status.Should().Be("running");
        retried.Attempts.Should().Be(2);
        retried.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Expired_Cleanup_Should_Be_Reclaimed_With_A_New_Lease()
    {
        using var factory = new PausedJobsFactory();
        var job = CreateExpiredJob(Guid.CreateVersion7(), "cleanup");
        var oldLease = job.LeaseId;
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AssetDbContext>();
            db.AssetProcessingJobs.Add(job);
            await db.SaveChangesAsync();
        }

        using var claim = factory.Services.CreateScope();
        var reclaimed = await claim.ServiceProvider.GetRequiredService<AssetProcessingQueue>().ClaimAsync(null, CancellationToken.None);

        reclaimed!.Id.Should().Be(job.Id);
        reclaimed.Status.Should().Be("running");
        reclaimed.Attempts.Should().Be(2);
        reclaimed.LeaseId.Should().NotBeNull();
        reclaimed.LeaseId!.Value.Should().NotBe(oldLease!.Value);
        reclaimed.LeaseExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Cleanup_Failure_Should_Keep_Pending_And_Retry_Already_Deleted_Targets_Safely()
    {
        using var factory = new PausedJobsFactory();
        factory.Storage.FailingKey = "second.png";
        factory.Storage.FailuresRemaining = 1;
        Guid jobId;
        using (var seed = factory.Services.CreateScope())
        {
            jobId = await seed.ServiceProvider.GetRequiredService<AssetProcessingQueue>().EnqueueAsync(
                Guid.CreateVersion7(), null, [new("local", "first.png"), new("local", "second.png")]);
        }

        using (var run = factory.Services.CreateScope())
        {
            var queue = run.ServiceProvider.GetRequiredService<AssetProcessingQueue>();
            await queue.RunAsync((await queue.ClaimAsync(jobId, CancellationToken.None))!, CancellationToken.None);
        }

        using (var pending = factory.Services.CreateScope())
        {
            var db = pending.ServiceProvider.GetRequiredService<AssetDbContext>();
            var job = await db.AssetProcessingJobs.SingleAsync(x => x.Id == jobId);
            job.Status.Should().Be("pending");
            job.ErrorMessage.Should().NotBeNullOrWhiteSpace();
            job.LeaseId.Should().BeNull();
            job.AvailableAtUtc.Should().BeAfter(job.UpdatedAtUtc);
            // 直接推进数据库中的可领取时间，无需等待重试退避。
            job.AvailableAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        using (var retry = factory.Services.CreateScope())
        {
            var queue = retry.ServiceProvider.GetRequiredService<AssetProcessingQueue>();
            await queue.RunAsync((await queue.ClaimAsync(jobId, CancellationToken.None))!, CancellationToken.None);
        }

        using var inspection = factory.Services.CreateScope();
        var succeeded = await inspection.ServiceProvider.GetRequiredService<AssetDbContext>()
            .AssetProcessingJobs.SingleAsync(x => x.Id == jobId);
        succeeded.Status.Should().Be("succeeded");
        succeeded.Attempts.Should().Be(2);
        succeeded.ErrorMessage.Should().BeNull();
        factory.Storage.Deletions.Should().Equal("first.png", "second.png", "first.png", "second.png");
    }

    [Fact]
    public async Task Pending_Cleanup_Should_Protect_Profile_Location_And_Prevent_Physical_Deletion()
    {
        using var factory = new PausedJobsFactory();
        var profile = new StorageProviderProfile(Guid.CreateVersion7(), "cleanup-profile", StorageProviderTypes.Local,
            JsonSerializer.Serialize(new { rootPath = Path.Combine(factory.TestStoragePath, "cleanup") }),
            StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.Local));
        var asset = CreateAsset(profile.Id);
        Guid cleanupId;
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AssetDbContext>();
            db.StorageProviderProfiles.Add(profile);
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
            db.Assets.Remove(asset);
            cleanupId = await seed.ServiceProvider.GetRequiredService<AssetProcessingQueue>().EnqueueAsync(
                asset.Id, profile.Id, [new("local", asset.StorageKey)]);
        }

        using (var manage = factory.Services.CreateScope())
        {
            var profiles = manage.ServiceProvider.GetRequiredService<IStorageProviderProfileManagementService>();
            var configuration = JsonSerializer.SerializeToElement(new { rootPath = Path.Combine(factory.TestStoragePath, "changed") });
            var changeLocation = () => profiles.UpdateAsync(new UpdateStorageProviderProfileCommand(profile.Id,
                default, default, default, OptionalValue<JsonElement?>.From(configuration), default));
            (await changeLocation.Should().ThrowAsync<ConflictException>()).Which.Code
                .Should().Be("storage_provider_profile_location_in_use");
            (await profiles.DeleteAsync(profile.Id)).Status.Should().Be("disabled");
            var db = manage.ServiceProvider.GetRequiredService<AssetDbContext>();
            (await db.Assets.AnyAsync(x => x.Id == asset.Id)).Should().BeFalse();
            (await db.StorageProviderProfiles.AnyAsync(x => x.Id == profile.Id)).Should().BeTrue();
        }

        using (var cleanup = factory.Services.CreateScope())
        {
            var queue = cleanup.ServiceProvider.GetRequiredService<AssetProcessingQueue>();
            await queue.RunAsync((await queue.ClaimAsync(cleanupId, CancellationToken.None))!, CancellationToken.None);
        }

        using var final = factory.Services.CreateScope();
        (await final.ServiceProvider.GetRequiredService<IStorageProviderProfileManagementService>().DeleteAsync(profile.Id))
            .Status.Should().Be("deleted");
    }

    private static Asset CreateAsset(Guid? profileId = null)
    {
        var asset = new Asset(Guid.CreateVersion7(), AssetType.Image, "job.png", "image/png", ".png", 10,
            "local", $"jobs/{Guid.CreateVersion7()}.png", storageProviderProfileId: profileId);
        asset.MarkReady();
        return asset;
    }

    private static AssetProcessingRequest CreateRequest(Asset asset)
    {
        return new AssetProcessingRequest(new AssetCreatedProcessingContext(asset.Id, asset.StorageProvider,
            asset.StorageKey, asset.ContentType, asset.Extension, asset.Size, asset.Width, asset.Height,
            asset.ChecksumSha256, asset.PublicUrl, asset.CreatedAtUtc), "test", JobId: Guid.CreateVersion7());
    }

    private static AssetProcessingJob CreateExpiredJob(Guid assetId, string kind)
    {
        return new AssetProcessingJob
        {
            Id = Guid.CreateVersion7(), AssetId = assetId, Kind = kind, Status = "running", Attempts = 1,
            PayloadJson = "[]", LeaseId = Guid.NewGuid(), LeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
    }

    private sealed class PausedJobsFactory : NekoHubApplicationFactory
    {
        public RecordingDispatcher Dispatcher { get; } = new();
        public RetryableStorage Storage { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                foreach (var registration in services.Where(service => service.ServiceType == typeof(IHostedService)
                    && service.ImplementationType == typeof(QueuedAssetProcessingWorker)).ToList())
                {
                    services.Remove(registration);
                }

                services.RemoveAll<IAssetProcessingDispatcher>();
                services.AddSingleton<IAssetProcessingDispatcher>(Dispatcher);
                services.RemoveAll<IAssetStorageTargetSelector>();
                services.AddSingleton<IAssetStorageTargetSelector>(new TestStorageSelector(Storage));
            });
        }
    }

    private sealed class RecordingDispatcher : IAssetProcessingDispatcher
    {
        public ConcurrentQueue<AssetProcessingRequest> Requests { get; } = new();

        public Task DispatchAsync(AssetProcessingRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Enqueue(request);
            return Task.CompletedTask;
        }
    }

    private sealed class TestStorageSelector(RetryableStorage storage) : IAssetStorageTargetSelector
    {
        public Task<AssetStorageTargetSelectionResult> ResolveWriteTargetAsync(Guid? requestedProfileId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AssetStorageLease> ResolveReadTargetAsync(Guid? boundProfileId, string legacyStorageProvider,
            CancellationToken cancellationToken = default) => Task.FromResult(AssetStorageLease.Shared(storage));
    }

    private sealed class RetryableStorage : IAssetStorage
    {
        public string ProviderName => "local";
        public string ProviderType => StorageProviderTypes.Local;
        public StorageProviderCapabilities Capabilities => StorageProviderCapabilityCatalog.GetRequired(ProviderType);
        public bool SupportsWrite => true;
        public string? FailingKey { get; set; }
        public int FailuresRemaining { get; set; }
        public List<string> Deletions { get; } = [];

        public Task DeleteAsync(DeleteStoredAssetRequest request, CancellationToken cancellationToken = default)
        {
            Deletions.Add(request.StorageKey);
            if (request.StorageKey == FailingKey && FailuresRemaining-- > 0)
            {
                throw new IOException("Temporary storage failure.");
            }
            return Task.CompletedTask;
        }

        public Task<StoredAssetObject> StoreAsync(Stream content, StoreAssetRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoredAssetObject> OverwriteAsync(Stream content, string storageKey, StoreAssetRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetPublicUrlAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

using FluentAssertions;
using NekoHub.Application.Abstractions.Metadata;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Abstractions.Skills;
using NekoHub.Application.Assets.Commands;
using NekoHub.Application.Assets.Dtos;
using NekoHub.Application.Assets.Queries;
using NekoHub.Application.Assets.Queries.Dtos;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Models;
using NekoHub.Domain.Assets;
using NekoHub.Domain.Storage;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Unit;

public class AssetCommandServiceTests
{
    private const string DefaultCommitMessage = "Automated commit by NekoHub";

    [Fact]
    public async Task Upload_With_RunEnrichment_False_Should_Skip_Queue()
    {
        var queue = new FakeAssetProcessingQueue();
        var service = CreateService(queue);

        await using var content = new MemoryStream([1, 2, 3, 4]);
        var result = await service.UploadAsync(new UploadAssetCommand(
            Content: content,
            OriginalFileName: "test.png",
            DeclaredContentType: "image/png",
            DeclaredSize: content.Length,
            CommitMessage: null,
            Description: "sample",
            AltText: "alt",
            IsPublic: true,
            StorageProviderProfileId: null,
            RunEnrichment: false));

        result.Should().NotBeNull();
        queue.EnqueueCount.Should().Be(0);
    }

    [Fact]
    public async Task Upload_With_RunEnrichment_True_Should_Enqueue()
    {
        var queue = new FakeAssetProcessingQueue();
        var service = CreateService(queue);

        await using var content = new MemoryStream([5, 6, 7, 8]);
        var result = await service.UploadAsync(new UploadAssetCommand(
            Content: content,
            OriginalFileName: "test.png",
            DeclaredContentType: "image/png",
            DeclaredSize: content.Length,
            CommitMessage: null,
            Description: null,
            AltText: null,
            IsPublic: true,
            StorageProviderProfileId: null,
            RunEnrichment: true));

        result.Should().NotBeNull();
        queue.EnqueueCount.Should().Be(1);
        queue.LastRequest.Should().NotBeNull();
        queue.LastRequest!.Asset.AssetId.Should().Be(result.Id);
        queue.LastRequest.TriggerSource.Should().Be(SkillTriggerSources.Upload);
    }

    [Fact]
    public async Task Upload_Without_Commit_Message_Should_Use_Default_Commit_Message()
    {
        var queue = new FakeAssetProcessingQueue();
        var storage = new FakeAssetStorage();
        var service = CreateService(queue, storage);

        await using var content = new MemoryStream([9, 9, 9, 9]);
        await service.UploadAsync(new UploadAssetCommand(
            Content: content,
            OriginalFileName: "default-commit.png",
            DeclaredContentType: "image/png",
            DeclaredSize: content.Length,
            CommitMessage: null,
            Description: null,
            AltText: null,
            IsPublic: true,
            StorageProviderProfileId: null,
            RunEnrichment: false));

        storage.LastStoreRequest.Should().NotBeNull();
        storage.LastStoreRequest!.CommitMessage.Should().Be(DefaultCommitMessage);
    }

    [Fact]
    public async Task Upload_With_Custom_Commit_Message_Should_Propagate_To_Storage()
    {
        var queue = new FakeAssetProcessingQueue();
        var storage = new FakeAssetStorage();
        var service = CreateService(queue, storage);

        await using var content = new MemoryStream([8, 8, 8, 8]);
        await service.UploadAsync(new UploadAssetCommand(
            Content: content,
            OriginalFileName: "custom-commit.png",
            DeclaredContentType: "image/png",
            DeclaredSize: content.Length,
            CommitMessage: "upload via tests",
            Description: null,
            AltText: null,
            IsPublic: true,
            StorageProviderProfileId: null,
            RunEnrichment: false));

        storage.LastStoreRequest.Should().NotBeNull();
        storage.LastStoreRequest!.CommitMessage.Should().Be("upload via tests");
    }

    [Fact]
    public async Task Delete_Without_Commit_Message_Should_Use_Default_Commit_Message()
    {
        var queue = new FakeAssetProcessingQueue();
        var storage = new FakeAssetStorage();
        var service = CreateService(queue, storage);

        await using var content = new MemoryStream([7, 7, 7, 7]);
        var uploaded = await service.UploadAsync(new UploadAssetCommand(
            Content: content,
            OriginalFileName: "delete-default.png",
            DeclaredContentType: "image/png",
            DeclaredSize: content.Length,
            CommitMessage: null,
            Description: null,
            AltText: null,
            IsPublic: true,
            StorageProviderProfileId: null,
            RunEnrichment: false));

        await service.DeleteAsync(new DeleteAssetCommand(uploaded.Id));

        storage.DeleteRequests.Should().ContainSingle();
        storage.DeleteRequests[0].CommitMessage.Should().Be(DefaultCommitMessage);
    }

    [Fact]
    public async Task Delete_With_Custom_Commit_Message_Should_Propagate_To_Storage()
    {
        var queue = new FakeAssetProcessingQueue();
        var storage = new FakeAssetStorage();
        var service = CreateService(queue, storage);

        await using var content = new MemoryStream([6, 6, 6, 6]);
        var uploaded = await service.UploadAsync(new UploadAssetCommand(
            Content: content,
            OriginalFileName: "delete-custom.png",
            DeclaredContentType: "image/png",
            DeclaredSize: content.Length,
            CommitMessage: null,
            Description: null,
            AltText: null,
            IsPublic: true,
            StorageProviderProfileId: null,
            RunEnrichment: false));

        await service.DeleteAsync(new DeleteAssetCommand(uploaded.Id, "delete via tests"));

        storage.DeleteRequests.Should().ContainSingle();
        storage.DeleteRequests[0].CommitMessage.Should().Be("delete via tests");
    }

    private static AssetCommandService CreateService(
        FakeAssetProcessingQueue queue,
        FakeAssetStorage? storage = null,
        FakeAssetRepository? assetRepository = null)
    {
        var resolvedStorage = storage ?? new FakeAssetStorage();
        var resolvedAssetRepository = assetRepository ?? new FakeAssetRepository();

        return new AssetCommandService(
            resolvedAssetRepository,
            new FakeAssetDerivativeRepository(),
            new FakeAssetStorageTargetSelector(resolvedStorage),
            new FakeAssetMetadataExtractor(),
            queue);
    }

    private sealed class FakeAssetRepository : IAssetRepository
    {
        private readonly List<Asset> _items = [];

        public Task AddAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            _items.Add(asset);
            return Task.CompletedTask;
        }

        public Task<Asset?> GetByIdAsync(Guid assetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.SingleOrDefault(item => item.Id == assetId));
        }

        public Task<Asset?> GetPublicByIdAsync(Guid assetId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Asset?> GetByStorageKeyAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.SingleOrDefault(item => item.StorageKey == storageKey));
        }

        public Task<bool> AnyByStorageProviderProfileIdAsync(Guid storageProviderProfileId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.StorageProviderProfileId == storageProviderProfileId));
        }

        public Task<PagedResult<Asset>> GetPagedAsync(GetAssetsPagedQuery query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PagedResult<Asset>> GetPublicPagedAsync(GetAssetsPagedQuery query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AssetUsageStatsQueryDto> GetUsageStatsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            _items.Remove(asset);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAssetDerivativeRepository : IAssetDerivativeRepository
    {
        public Task<AssetDerivative?> GetBySourceAndKindAsync(Guid sourceAssetId, string kind, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<AssetDerivative>> GetBySourceAssetIdAsync(Guid sourceAssetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AssetDerivative>>([]);
        }

        public Task<AssetDerivative?> GetByStorageKeyAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(AssetDerivative derivative, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteRangeAsync(IEnumerable<AssetDerivative> derivatives, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAssetStorageTargetSelector(FakeAssetStorage storage) : IAssetStorageTargetSelector
    {
        public Task<AssetStorageTargetSelectionResult> ResolveWriteTargetAsync(
            Guid? requestedProfileId,
            CancellationToken cancellationToken = default)
        {
            var result = new AssetStorageTargetSelectionResult(
                AssetStorageLease.Shared(storage),
                requestedProfileId,
                "test");
            return Task.FromResult(result);
        }

        public Task<AssetStorageLease> ResolveReadTargetAsync(
            Guid? boundProfileId,
            string legacyStorageProvider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AssetStorageLease.Shared(storage));
        }
    }

    private sealed class FakeAssetStorage : IAssetStorage
    {
        public StoreAssetRequest? LastStoreRequest { get; private set; }

        public List<DeleteStoredAssetRequest> DeleteRequests { get; } = [];

        public string ProviderName => "local";

        public string ProviderType => StorageProviderTypes.Local;

        public StorageProviderCapabilities Capabilities =>
            new(true, true, true, true, true, false, true, false, false, false);

        public bool SupportsWrite => true;

        public Task<StoredAssetObject> StoreAsync(
            Stream content,
            StoreAssetRequest request,
            CancellationToken cancellationToken = default)
        {
            LastStoreRequest = request;
            return Task.FromResult(new StoredAssetObject(
                Provider: ProviderName,
                StorageKey: $"stored/{request.FileName}",
                PublicUrl: $"https://cdn.example.com/{request.FileName}",
                StoredFileName: request.FileName));
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StoredAssetObject> OverwriteAsync(
            Stream content,
            string storageKey,
            StoreAssetRequest request,
            CancellationToken cancellationToken = default)
        {
            LastStoreRequest = request;
            return Task.FromResult(new StoredAssetObject(
                Provider: ProviderName,
                StorageKey: storageKey,
                PublicUrl: $"https://cdn.example.com/{storageKey}",
                StoredFileName: Path.GetFileName(storageKey)));
        }

        public Task DeleteAsync(DeleteStoredAssetRequest request, CancellationToken cancellationToken = default)
        {
            DeleteRequests.Add(request);
            return Task.CompletedTask;
        }

        public Task<string?> GetPublicUrlAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>($"https://cdn.example.com/{storageKey}");
        }
    }

    private sealed class FakeAssetMetadataExtractor : IAssetMetadataExtractor
    {
        public Task<ExtractedAssetMetadata> ExtractAsync(
            Stream content,
            string? originalFileName,
            string? declaredContentType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExtractedAssetMetadata(
                ContentType: declaredContentType,
                Size: content.Length,
                Width: 2,
                Height: 2,
                Extension: ".png",
                ChecksumSha256: null));
        }
    }

    private sealed class FakeAssetProcessingQueue : IAssetProcessingQueue
    {
        public int EnqueueCount { get; private set; }

        public AssetProcessingRequest? LastRequest { get; private set; }

        public ValueTask EnqueueAsync(
            AssetProcessingRequest request,
            CancellationToken cancellationToken = default)
        {
            EnqueueCount += 1;
            LastRequest = request;
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<AssetProcessingRequest> DequeueAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}

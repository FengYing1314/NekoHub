using NekoHub.Domain.Storage;

namespace NekoHub.Application.Abstractions.Persistence;

public interface IStorageProviderProfileRepository
{
    Task AddAsync(StorageProviderProfile profile, CancellationToken cancellationToken = default);

    Task<StorageProviderProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<StorageProviderProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeProfileId = null, CancellationToken cancellationToken = default);

    Task<StorageProviderProfile?> GetDefaultAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StorageProviderProfile>> ListDefaultProfilesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StorageProviderProfile>> ListAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(StorageProviderProfile profile, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

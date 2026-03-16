using NekoHub.Domain.Ai;

namespace NekoHub.Application.Abstractions.Persistence;

public interface IAiProviderProfileRepository
{
    Task AddAsync(AiProviderProfile profile, CancellationToken cancellationToken = default);

    Task<AiProviderProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<AiProviderProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<AiProviderProfile?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeProfileId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiProviderProfile>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiProviderProfile>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(AiProviderProfile profile, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

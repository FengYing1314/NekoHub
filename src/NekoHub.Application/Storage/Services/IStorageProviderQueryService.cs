using NekoHub.Application.Storage.Queries.Dtos;

namespace NekoHub.Application.Storage.Services;

public interface IStorageProviderQueryService
{
    Task<StorageProviderOverviewQueryDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}

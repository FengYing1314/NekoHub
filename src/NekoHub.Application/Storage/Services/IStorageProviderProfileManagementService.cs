using NekoHub.Application.Storage.Commands;
using NekoHub.Application.Storage.Dtos;
using NekoHub.Application.Storage.Queries.Dtos;

namespace NekoHub.Application.Storage.Services;

public interface IStorageProviderProfileManagementService
{
    Task<StorageProviderProfileQueryDto> CreateAsync(CreateStorageProviderProfileCommand command, CancellationToken cancellationToken = default);

    Task<StorageProviderProfileQueryDto> UpdateAsync(UpdateStorageProviderProfileCommand command, CancellationToken cancellationToken = default);

    Task<DeleteStorageProviderProfileResultDto> DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<StorageProviderProfileQueryDto> SetDefaultAsync(Guid profileId, CancellationToken cancellationToken = default);
}

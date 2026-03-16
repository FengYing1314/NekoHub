using NekoHub.Application.Storage.Dtos;

namespace NekoHub.Application.Storage.Services;

public interface IGitHubRepoProfileAccessService
{
    Task<GitHubRepoBrowseProfileResultDto> BrowseAsync(
        Guid profileId,
        GitHubRepoBrowseProfileRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GitHubRepoUpsertProfileResultDto> UpsertAsync(
        Guid profileId,
        GitHubRepoUpsertProfileRequestDto request,
        CancellationToken cancellationToken = default);
}

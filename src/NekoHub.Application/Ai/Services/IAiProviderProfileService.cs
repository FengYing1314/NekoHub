using NekoHub.Application.Ai.Commands;
using NekoHub.Application.Ai.Dtos;
using NekoHub.Application.Ai.Queries.Dtos;

namespace NekoHub.Application.Ai.Services;

public interface IAiProviderProfileService
{
    Task<IReadOnlyList<AiProviderProfileQueryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<AiProviderProfileQueryDto?> GetActiveProfileAsync(CancellationToken cancellationToken = default);

    Task<AiProviderRuntimeProfileDto?> GetActiveRuntimeProfileAsync(CancellationToken cancellationToken = default);

    Task<AiProviderProfileQueryDto> CreateAsync(CreateAiProviderProfileCommand command, CancellationToken cancellationToken = default);

    Task<AiProviderProfileQueryDto> UpdateAsync(UpdateAiProviderProfileCommand command, CancellationToken cancellationToken = default);

    Task<DeleteAiProviderProfileResultDto> DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);
}

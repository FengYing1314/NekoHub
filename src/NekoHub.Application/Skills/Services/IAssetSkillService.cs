using NekoHub.Application.Skills.Dtos;

namespace NekoHub.Application.Skills.Services;

public interface IAssetSkillService
{
    Task<IReadOnlyList<AssetSkillSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<RunAssetSkillResultDto> RunAsync(
        Guid assetId,
        string skillName,
        CancellationToken cancellationToken = default);
}

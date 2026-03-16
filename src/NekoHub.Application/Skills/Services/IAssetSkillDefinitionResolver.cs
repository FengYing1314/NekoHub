using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;

namespace NekoHub.Application.Skills.Services;

public interface IAssetSkillDefinitionResolver
{
    IReadOnlyList<SkillDefinition> GetDefaultForAssetCreated(AssetCreatedProcessingContext context);

    SkillDefinition? ResolveForAsset(AssetCreatedProcessingContext context, string skillId);
}

using NekoHub.Application.Abstractions.Processing;

namespace NekoHub.Application.Abstractions.Skills;

public interface IAssetSkillDefinitionProvider
{
    IReadOnlyList<SkillDefinition> GetAll();

    IReadOnlyList<SkillDefinition> GetForAssetCreated(AssetCreatedProcessingContext context);
}

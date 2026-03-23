using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;

namespace NekoHub.Infrastructure.Processing;

public sealed class AssetProcessingDispatcher(
    IAssetSkillDefinitionProvider skillDefinitionProvider,
    ISkillRunner skillRunner,
    ILogger<AssetProcessingDispatcher> logger) : IAssetProcessingDispatcher
{
    public async Task DispatchAssetCreatedAsync(
        AssetCreatedProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        var skillContext = new SkillExecutionContext(context, SkillTriggerSources.Upload);
        var skills = skillDefinitionProvider
            .GetForAssetCreated(context)
            .OrderBy(static skill => skill.Order)
            .ThenBy(static skill => skill.Name, StringComparer.Ordinal);

        foreach (var skill in skills)
        {
            try
            {
                var runResult = await skillRunner.RunAsync(skill, skillContext, cancellationToken);
                if (!runResult.Succeeded)
                {
                    logger.LogWarning(
                        "Skill finished with failed steps. Skill={SkillName}, AssetId={AssetId}",
                        skill.Name,
                        context.AssetId);
                }
            }
            catch (Exception exception)
            {
                // Skill 管线语义：单个 skill 失败不影响上传主链路，继续执行后续 skill。
                logger.LogError(
                    exception,
                    "Skill execution failed. Skill={SkillName}, AssetId={AssetId}",
                    skill.Name,
                    context.AssetId);
            }
        }
    }
}

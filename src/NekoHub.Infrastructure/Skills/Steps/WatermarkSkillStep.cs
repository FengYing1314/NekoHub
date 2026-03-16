using NekoHub.Application.Abstractions.Skills;
using NekoHub.Infrastructure.Processing;

namespace NekoHub.Infrastructure.Skills.Steps;

public sealed class WatermarkSkillStep(
    WatermarkAssetPostProcessor watermarkAssetPostProcessor) : ISkillStepExecutor
{
    public string StepName => "draw_watermark";

    public Task ExecuteAsync(SkillExecutionContext context, CancellationToken cancellationToken = default)
    {
        return watermarkAssetPostProcessor.ProcessAsync(
            context.Asset,
            context.Parameters,
            cancellationToken);
    }
}

using NekoHub.Application.Abstractions.Skills;
using NekoHub.Infrastructure.Processing;

namespace NekoHub.Infrastructure.Skills.Steps;

public sealed class GenerateThumbnailSkillStep(
    ThumbnailAssetPostProcessor thumbnailAssetPostProcessor) : ISkillStepExecutor
{
    public string StepName => "generate_thumbnail";

    public Task ExecuteAsync(SkillExecutionContext context, CancellationToken cancellationToken = default)
    {
        return thumbnailAssetPostProcessor.ProcessAsync(context.Asset, cancellationToken);
    }
}

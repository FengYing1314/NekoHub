using NekoHub.Application.Abstractions.Skills;
using NekoHub.Infrastructure.Processing;

namespace NekoHub.Infrastructure.Skills.Steps;

public sealed class ExifStripSkillStep(
    ExifStripAssetPostProcessor exifStripAssetPostProcessor) : ISkillStepExecutor
{
    public string StepName => "strip_exif_metadata";

    public Task ExecuteAsync(SkillExecutionContext context, CancellationToken cancellationToken = default)
    {
        return exifStripAssetPostProcessor.ProcessAsync(context.Asset, cancellationToken);
    }
}

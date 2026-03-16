using NekoHub.Application.Abstractions.Skills;
using NekoHub.Infrastructure.Processing;

namespace NekoHub.Infrastructure.Skills.Steps;

public sealed class FormatConvertSkillStep(
    FormatConvertAssetPostProcessor formatConvertAssetPostProcessor) : ISkillStepExecutor
{
    public string StepName => "convert_image_format";

    public Task ExecuteAsync(SkillExecutionContext context, CancellationToken cancellationToken = default)
    {
        return formatConvertAssetPostProcessor.ProcessAsync(
            context.Asset,
            context.Parameters,
            cancellationToken);
    }
}

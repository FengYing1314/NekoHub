using NekoHub.Application.Abstractions.Skills;
using NekoHub.Infrastructure.Processing;

namespace NekoHub.Infrastructure.Skills.Steps;

public sealed class GenerateBasicCaptionSkillStep(
    BasicCaptionStructuredResultPostProcessor basicCaptionPostProcessor) : ISkillStepExecutor
{
    public string StepName => "generate_basic_caption";

    public Task ExecuteAsync(SkillExecutionContext context, CancellationToken cancellationToken = default)
    {
        return basicCaptionPostProcessor.ProcessAsync(context.Asset, cancellationToken);
    }
}

namespace NekoHub.Application.Abstractions.Skills;

public interface ISkillStepExecutor
{
    string StepName { get; }

    Task ExecuteAsync(SkillExecutionContext context, CancellationToken cancellationToken = default);
}

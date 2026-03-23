namespace NekoHub.Application.Abstractions.Skills;

public interface ISkillRunner
{
    Task<SkillRunResult> RunAsync(
        SkillDefinition definition,
        SkillExecutionContext context,
        CancellationToken cancellationToken = default);
}

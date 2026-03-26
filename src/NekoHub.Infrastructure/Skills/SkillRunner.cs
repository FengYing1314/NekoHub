using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Skills;
using NekoHub.Domain.Skills;

namespace NekoHub.Infrastructure.Skills;

public sealed class SkillRunner(
    IEnumerable<ISkillStepExecutor> stepExecutors,
    IAssetSkillExecutionRepository skillExecutionRepository,
    ILogger<SkillRunner> logger) : ISkillRunner
{
    private readonly IReadOnlyDictionary<string, ISkillStepExecutor> _stepExecutors = stepExecutors
        .ToDictionary(static step => step.StepName, StringComparer.Ordinal);

    public async Task<SkillRunResult> RunAsync(
        SkillDefinition definition,
        SkillExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.CreateVersion7();
        var runStartedAtUtc = DateTimeOffset.UtcNow;
        var stepResults = new List<SkillStepRunResult>(definition.Steps.Count);
        var stepExecutionRecords = new List<SkillExecutionStepResult>(definition.Steps.Count);

        foreach (var step in definition.Steps)
        {
            var stepStartedAtUtc = DateTimeOffset.UtcNow;
            if (!_stepExecutors.TryGetValue(step.Name, out var executor))
            {
                logger.LogWarning(
                    "Skill step is not registered. Skill={SkillName}, Step={StepName}, AssetId={AssetId}",
                    definition.Name,
                    step.Name,
                    context.Asset.AssetId);
                stepResults.Add(new SkillStepRunResult(
                    Name: step.Name,
                    Succeeded: false,
                    ErrorMessage: "Step executor is not registered."));
                stepExecutionRecords.Add(new SkillExecutionStepResult(
                    id: Guid.CreateVersion7(),
                    skillExecutionId: executionId,
                    stepName: step.Name,
                    succeeded: false,
                    errorMessage: "Step executor is not registered.",
                    startedAtUtc: stepStartedAtUtc,
                    completedAtUtc: DateTimeOffset.UtcNow));
                continue;
            }

            try
            {
                await executor.ExecuteAsync(context, cancellationToken);
                var stepCompletedAtUtc = DateTimeOffset.UtcNow;
                stepResults.Add(new SkillStepRunResult(
                    Name: step.Name,
                    Succeeded: true));
                stepExecutionRecords.Add(new SkillExecutionStepResult(
                    id: Guid.CreateVersion7(),
                    skillExecutionId: executionId,
                    stepName: step.Name,
                    succeeded: true,
                    errorMessage: null,
                    startedAtUtc: stepStartedAtUtc,
                    completedAtUtc: stepCompletedAtUtc));
            }
            catch (Exception exception)
            {
                // Skill 阶段沿用处理骨架语义：步骤失败只记录，不影响上传主链路，也不中断后续步骤。
                logger.LogError(
                    exception,
                    "Skill step execution failed. Skill={SkillName}, Step={StepName}, AssetId={AssetId}",
                    definition.Name,
                    step.Name,
                    context.Asset.AssetId);
                stepResults.Add(new SkillStepRunResult(
                    Name: step.Name,
                    Succeeded: false,
                    ErrorMessage: $"Step '{step.Name}' failed."));
                stepExecutionRecords.Add(new SkillExecutionStepResult(
                    id: Guid.CreateVersion7(),
                    skillExecutionId: executionId,
                    stepName: step.Name,
                    succeeded: false,
                    errorMessage: $"Step '{step.Name}' failed.",
                    startedAtUtc: stepStartedAtUtc,
                    completedAtUtc: DateTimeOffset.UtcNow));
            }
        }

        var runCompletedAtUtc = DateTimeOffset.UtcNow;
        var succeeded = stepResults.All(static result => result.Succeeded);
        var execution = new SkillExecution(
            id: executionId,
            sourceAssetId: context.Asset.AssetId,
            skillName: definition.Name,
            triggerSource: context.TriggerSource,
            startedAtUtc: runStartedAtUtc,
            completedAtUtc: runCompletedAtUtc,
            succeeded: succeeded,
            parametersJson: SerializeParameters(context.Parameters));

        await skillExecutionRepository.AddExecutionAsync(execution, cancellationToken);
        await skillExecutionRepository.AddStepResultsAsync(stepExecutionRecords, cancellationToken);
        await skillExecutionRepository.SaveChangesAsync(cancellationToken);

        return new SkillRunResult(
            SkillName: definition.Name,
            Succeeded: succeeded,
            Steps: stepResults);
    }

    private static string? SerializeParameters(JsonObject? parameters)
    {
        return parameters?.ToJsonString();
    }
}

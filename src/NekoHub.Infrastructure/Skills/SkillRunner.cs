using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;
using NekoHub.Application.Common.Diagnostics;
using NekoHub.Domain.Skills;

namespace NekoHub.Infrastructure.Skills;

public sealed class SkillRunner(
    IServiceScopeFactory serviceScopeFactory,
    IAssetMutationLock assetMutationLock,
    ILogger<SkillRunner> logger) : ISkillRunner
{
    private const int MaxStepErrorMessageLength = 2048;

    public async Task<SkillRunResult> RunAsync(
        SkillDefinition definition,
        SkillExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await assetMutationLock.AcquireAsync(context.Asset.AssetId, cancellationToken);
        var executionId = Guid.CreateVersion7();
        var runStartedAtUtc = DateTimeOffset.UtcNow;
        var stepResults = new List<SkillStepRunResult>(definition.Steps.Count);
        var stepExecutionRecords = new List<SkillExecutionStepResult>(definition.Steps.Count);

        foreach (var step in definition.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stepStartedAtUtc = DateTimeOffset.UtcNow;
            // 步骤拥有独立工作单元，失败后丢弃其跟踪状态，不污染后续步骤或执行日志。
            await using var stepScope = serviceScopeFactory.CreateAsyncScope();
            var executor = stepScope.ServiceProvider.GetServices<ISkillStepExecutor>()
                .SingleOrDefault(candidate => string.Equals(candidate.StepName, step.Name, StringComparison.Ordinal));
            if (executor is null)
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
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
                var publicErrorMessage = PublicErrorMessageSanitizer.Sanitize(
                    exception,
                    MaxStepErrorMessageLength,
                    $"Step '{step.Name}' failed.");
                stepResults.Add(new SkillStepRunResult(
                    Name: step.Name,
                    Succeeded: false,
                    ErrorMessage: publicErrorMessage));
                stepExecutionRecords.Add(new SkillExecutionStepResult(
                    id: Guid.CreateVersion7(),
                    skillExecutionId: executionId,
                    stepName: step.Name,
                    succeeded: false,
                    errorMessage: publicErrorMessage,
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

        await using var recordScope = serviceScopeFactory.CreateAsyncScope();
        var skillExecutionRepository = recordScope.ServiceProvider.GetRequiredService<IAssetSkillExecutionRepository>();
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

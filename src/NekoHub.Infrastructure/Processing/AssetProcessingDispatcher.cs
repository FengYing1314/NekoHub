using Microsoft.Extensions.Logging;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Skills.Services;
using NekoHub.Application.Workflows.Parsing;

namespace NekoHub.Infrastructure.Processing;

public sealed class AssetProcessingDispatcher(
    IWorkflowProfileRepository workflowProfileRepository,
    IWorkflowGraphParser workflowGraphParser,
    IAssetSkillDefinitionResolver assetSkillDefinitionResolver,
    ISkillRunner skillRunner,
    ILogger<AssetProcessingDispatcher> logger) : IAssetProcessingDispatcher
{
    public async Task DispatchAsync(
        AssetProcessingRequest request,
        CancellationToken cancellationToken = default)
    {
        var skills = await ResolveSkillsAsync(request, cancellationToken);
        if (skills.Count == 0)
        {
            return;
        }

        foreach (var skill in skills)
        {
            try
            {
                var runResult = await skillRunner.RunAsync(
                    skill.Definition,
                    new SkillExecutionContext(request.Asset, request.TriggerSource, skill.Parameters),
                    cancellationToken);
                if (!runResult.Succeeded)
                {
                    logger.LogWarning(
                        "Skill finished with failed steps. Skill={SkillName}, AssetId={AssetId}",
                        skill.Definition.Name,
                        request.Asset.AssetId);
                }
            }
            catch (Exception exception)
            {
                // Skill 管线语义：单个 skill 失败不影响上传主链路，继续执行后续 skill。
                logger.LogError(
                    exception,
                    "Skill execution failed. Skill={SkillName}, AssetId={AssetId}",
                    skill.Definition.Name,
                    request.Asset.AssetId);
            }
        }
    }

    private async Task<IReadOnlyList<ResolvedSkillExecution>> ResolveSkillsAsync(
        AssetProcessingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Skills is { Count: > 0 })
        {
            // 手动触发或 MCP 显式指定 skill 时，以请求体中的 skill 列表为准，不再叠加自动运行配置。
            return ResolveRequestedSkills(request.Asset, request.Skills);
        }

        var workflow = await workflowProfileRepository.GetAutoRunAsync(cancellationToken);
        if (workflow is null)
        {
            // 未配置自动工作流时，回退到按资产类型解析出的默认创建后技能集合。
            return assetSkillDefinitionResolver.GetDefaultForAssetCreated(request.Asset)
                .Select(static skill => new ResolvedSkillExecution(skill, Parameters: null))
                .ToList();
        }

        IReadOnlyList<WorkflowSkillNodeDefinition> skills;
        try
        {
            skills = workflowGraphParser.ExtractSkills(workflow.GraphJson);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Skipping auto-run workflow because GraphJson is invalid. WorkflowId={WorkflowId}, AssetId={AssetId}",
                workflow.Id,
                request.Asset.AssetId);
            return [];
        }

        if (skills.Count == 0)
        {
            logger.LogInformation(
                "Skipping auto-run workflow because no executable skills were found. WorkflowId={WorkflowId}, AssetId={AssetId}",
                workflow.Id,
                request.Asset.AssetId);
            return [];
        }

        return ResolveRequestedSkills(
            request.Asset,
            skills.Select(skill => new AssetProcessingSkillRequest(skill.SkillId, skill.Parameters)).ToList());
    }

    private IReadOnlyList<ResolvedSkillExecution> ResolveRequestedSkills(
        AssetCreatedProcessingContext asset,
        IReadOnlyList<AssetProcessingSkillRequest> skills)
    {
        var resolvedSkills = new List<ResolvedSkillExecution>(skills.Count);

        foreach (var skillRequest in skills)
        {
            var skill = assetSkillDefinitionResolver.ResolveForAsset(asset, skillRequest.SkillId);
            if (skill is null)
            {
                // 运行时解析不到定义时直接跳过，避免单个脏节点让整条异步管线失效。
                logger.LogWarning(
                    "Skipping unresolved workflow skill. SkillId={SkillId}, AssetId={AssetId}",
                    skillRequest.SkillId,
                    asset.AssetId);
                continue;
            }

            resolvedSkills.Add(new ResolvedSkillExecution(skill, skillRequest.Parameters));
        }

        return resolvedSkills;
    }

    private sealed record ResolvedSkillExecution(
        SkillDefinition Definition,
        System.Text.Json.Nodes.JsonObject? Parameters);
}

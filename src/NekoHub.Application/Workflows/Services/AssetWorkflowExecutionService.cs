using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;
using NekoHub.Application.Assets.Queries.Dtos;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Skills.Services;
using NekoHub.Application.Workflows.Dtos;
using NekoHub.Application.Workflows.Parsing;

namespace NekoHub.Application.Workflows.Services;

public sealed class AssetWorkflowExecutionService(
    IAssetQueryService assetQueryService,
    IWorkflowProfileRepository workflowProfileRepository,
    IWorkflowGraphParser workflowGraphParser,
    IAssetSkillDefinitionResolver assetSkillDefinitionResolver,
    IAssetProcessingQueue assetProcessingQueue) : IAssetWorkflowExecutionService
{
    public async Task<QueuedWorkflowRunResultDto> QueueWorkflowAsync(
        Guid assetId,
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var asset = await assetQueryService.GetByIdAsync(assetId, cancellationToken);
        var processingContext = ToProcessingContext(asset);

        var workflow = await workflowProfileRepository.GetByIdAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            throw new NotFoundException(
                "workflow_profile_not_found",
                $"Workflow profile '{workflowId}' was not found.");
        }

        var skills = workflowGraphParser.ExtractSkills(workflow.GraphJson);
        if (skills.Count == 0)
        {
            throw new ValidationException(
                "workflow_profile_has_no_skills",
                $"Workflow profile '{workflowId}' does not define any executable skills.");
        }

        // 在真正入队前先按资产类型做一次静态能力校验，避免排队后才发现 workflow 中含有无法执行的 skill。
        var unsupportedSkillIds = skills
            .Select(skill => skill.SkillId)
            .Where(skillId => assetSkillDefinitionResolver.ResolveForAsset(processingContext, skillId) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unsupportedSkillIds.Count > 0)
        {
            throw new ValidationException(
                "workflow_profile_contains_unsupported_skills",
                $"Workflow profile '{workflowId}' contains unsupported skills for this asset: {string.Join(", ", unsupportedSkillIds)}.");
        }

        // 工作流运行接口只负责排队，不在 HTTP 请求线程中同步执行整条 skill 管线。
        await assetProcessingQueue.EnqueueAsync(
            new AssetProcessingRequest(
                Asset: processingContext,
                TriggerSource: SkillTriggerSources.Manual,
                WorkflowProfileId: workflow.Id,
                Skills: skills
                    .Select(skill => new AssetProcessingSkillRequest(skill.SkillId, skill.Parameters))
                    .ToList()),
            cancellationToken);

        return new QueuedWorkflowRunResultDto(
            AssetId: assetId,
            WorkflowId: workflow.Id,
            SkillIds: skills.Select(skill => skill.SkillId).ToList());
    }

    private static AssetCreatedProcessingContext ToProcessingContext(AssetDetailsQueryDto asset)
    {
        return new AssetCreatedProcessingContext(
            AssetId: asset.Id,
            StorageProvider: asset.StorageProvider,
            StorageKey: asset.StorageKey,
            ContentType: asset.ContentType,
            Extension: asset.Extension,
            Size: asset.Size,
            Width: asset.Width,
            Height: asset.Height,
            ChecksumSha256: asset.ChecksumSha256,
            PublicUrl: asset.PublicUrl,
            CreatedAtUtc: asset.CreatedAtUtc);
    }
}

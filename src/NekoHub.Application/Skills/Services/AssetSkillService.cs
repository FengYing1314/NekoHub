using System.Text.Json.Nodes;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;
using NekoHub.Application.Assets.Queries.Dtos;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Skills.Dtos;

namespace NekoHub.Application.Skills.Services;

public sealed class AssetSkillService(
    IAssetQueryService assetQueryService,
    IAssetSkillDefinitionProvider skillDefinitionProvider,
    ISkillRunner skillRunner) : IAssetSkillService
{
    public Task<IReadOnlyList<AssetSkillSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var skills = skillDefinitionProvider.GetAll()
            .OrderBy(static skill => skill.Order)
            .ThenBy(static skill => skill.Name, StringComparer.Ordinal)
            .Select(static skill => new AssetSkillSummaryDto(
                SkillName: skill.Name,
                Description: skill.Description,
                Steps: skill.Steps.Select(static step => step.Name).ToList()))
            .ToList();

        return Task.FromResult<IReadOnlyList<AssetSkillSummaryDto>>(skills);
    }

    public async Task<RunAssetSkillResultDto> RunAsync(
        Guid assetId,
        string skillName,
        JsonObject? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            throw new ValidationException("skill_name_required", "Skill name is required.");
        }

        var normalizedSkillName = skillName.Trim();
        var initialAsset = await assetQueryService.GetByIdAsync(assetId, cancellationToken);
        var processingContext = ToProcessingContext(initialAsset);
        var skill = skillDefinitionProvider
            .GetForAssetCreated(processingContext)
            .FirstOrDefault(definition => string.Equals(definition.Name, normalizedSkillName, StringComparison.OrdinalIgnoreCase));

        if (skill is null)
        {
            throw new NotFoundException(
                "skill_not_found",
                $"Skill '{normalizedSkillName}' was not found for asset '{assetId}'.");
        }

        var runResult = await skillRunner.RunAsync(
            skill,
            new SkillExecutionContext(processingContext, SkillTriggerSources.Manual, parameters),
            cancellationToken);

        var latestAsset = await assetQueryService.GetByIdAsync(assetId, cancellationToken);
        return new RunAssetSkillResultDto(
            Succeeded: runResult.Succeeded,
            SkillName: runResult.SkillName,
            Steps: runResult.Steps
                .Select(static step => new RunAssetSkillStepResultDto(
                    Name: step.Name,
                    Succeeded: step.Succeeded,
                    ErrorMessage: step.ErrorMessage))
                .ToList(),
            Asset: latestAsset);
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

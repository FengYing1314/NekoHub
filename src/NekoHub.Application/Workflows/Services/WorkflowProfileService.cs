using System.Text.Json;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Workflows.Dtos;
using NekoHub.Domain.Workflows;

namespace NekoHub.Application.Workflows.Services;

public sealed class WorkflowProfileService(IWorkflowProfileRepository workflowProfileRepository) : IWorkflowProfileService
{
    public async Task<IReadOnlyList<WorkflowProfileDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await workflowProfileRepository.ListAsync(cancellationToken);
        return profiles
            .Select(WorkflowProfileMapper.ToDto)
            .ToList();
    }

    public async Task<WorkflowProfileDto> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await workflowProfileRepository.GetByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "workflow_profile_not_found",
                $"Workflow profile '{profileId}' was not found.");
        }

        return WorkflowProfileMapper.ToDto(profile);
    }

    public async Task<WorkflowProfileDto> CreateAsync(
        CreateWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(request.Name);
        var description = NormalizeDescription(request.Description);
        var graphJson = NormalizeGraphJson(request.GraphJson);

        await EnsureUniqueNameAsync(name, null, cancellationToken);

        if (request.IsAutoRun)
        {
            await ClearAutoRunProfilesAndPersistAsync(excludeProfileId: null, cancellationToken);
        }

        var profile = new WorkflowProfile(
            id: Guid.CreateVersion7(),
            name: name,
            description: description,
            isAutoRun: request.IsAutoRun,
            graphJson: graphJson);

        await workflowProfileRepository.AddAsync(profile, cancellationToken);
        await workflowProfileRepository.SaveChangesAsync(cancellationToken);

        return WorkflowProfileMapper.ToDto(profile);
    }

    public async Task<WorkflowProfileDto> UpdateAsync(
        Guid profileId,
        UpdateWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await workflowProfileRepository.GetByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "workflow_profile_not_found",
                $"Workflow profile '{profileId}' was not found.");
        }

        var name = NormalizeName(request.Name);
        var description = NormalizeDescription(request.Description);
        var graphJson = NormalizeGraphJson(request.GraphJson);

        await EnsureUniqueNameAsync(name, profile.Id, cancellationToken);

        if (request.IsAutoRun)
        {
            await ClearAutoRunProfilesAndPersistAsync(profile.Id, cancellationToken);
        }

        profile.UpdateDefinition(name, description, graphJson);
        profile.SetAutoRun(request.IsAutoRun);

        await workflowProfileRepository.SaveChangesAsync(cancellationToken);
        return WorkflowProfileMapper.ToDto(profile);
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await workflowProfileRepository.GetByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "workflow_profile_not_found",
                $"Workflow profile '{profileId}' was not found.");
        }

        await workflowProfileRepository.DeleteAsync(profile, cancellationToken);
        await workflowProfileRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowProfileDto> SetAutoRunAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await workflowProfileRepository.GetByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(
                "workflow_profile_not_found",
                $"Workflow profile '{profileId}' was not found.");
        }

        await ClearAutoRunProfilesAndPersistAsync(profile.Id, cancellationToken);
        profile.SetAutoRun(true);
        await workflowProfileRepository.SaveChangesAsync(cancellationToken);

        return WorkflowProfileMapper.ToDto(profile);
    }

    private async Task EnsureUniqueNameAsync(string name, Guid? excludeProfileId, CancellationToken cancellationToken)
    {
        if (await workflowProfileRepository.ExistsByNameAsync(name, excludeProfileId, cancellationToken))
        {
            throw new ValidationException(
                "workflow_profile_name_conflict",
                $"Workflow profile name '{name}' already exists.");
        }
    }

    private async Task ClearAutoRunProfilesAsync(CancellationToken cancellationToken, Guid? excludeProfileId = null)
    {
        var autoRunProfiles = await workflowProfileRepository.ListAutoRunAsync(cancellationToken);
        foreach (var autoRunProfile in autoRunProfiles.Where(x => x.Id != excludeProfileId))
        {
            autoRunProfile.SetAutoRun(false);
        }
    }

    private async Task ClearAutoRunProfilesAndPersistAsync(Guid? excludeProfileId, CancellationToken cancellationToken)
    {
        // WorkflowProfiles 对 IsAutoRun=true 有唯一索引，先把旧的 auto-run 落库清掉，
        // 再把新的 workflow 置为 true，避免同一次 SaveChanges 的更新顺序撞唯一约束。
        await ClearAutoRunProfilesAsync(cancellationToken, excludeProfileId);
        await workflowProfileRepository.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(
                "workflow_profile_name_required",
                "Name is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > WorkflowProfile.NameMaxLength)
        {
            throw new ValidationException(
                "workflow_profile_name_too_long",
                $"Name must be {WorkflowProfile.NameMaxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > WorkflowProfile.DescriptionMaxLength)
        {
            throw new ValidationException(
                "workflow_profile_description_too_long",
                $"Description must be {WorkflowProfile.DescriptionMaxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string NormalizeGraphJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(
                "workflow_profile_graph_json_required",
                "GraphJson is required.");
        }

        var normalized = value.Trim();

        try
        {
            using var document = JsonDocument.Parse(normalized);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ValidationException(
                    "workflow_profile_graph_json_invalid",
                    "GraphJson must be a valid JSON object.");
            }

            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            throw new ValidationException(
                "workflow_profile_graph_json_invalid",
                "GraphJson must be a valid JSON object.");
        }
    }
}

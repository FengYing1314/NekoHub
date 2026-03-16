using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NekoHub.Api.Auth;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Auth;
using NekoHub.Application.Workflows.Dtos;
using NekoHub.Application.Workflows.Services;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/system/workflows")]
[Authorize(Policy = AuthorizationPolicies.ManagementAccess)]
public sealed class WorkflowsController(IWorkflowProfileService workflowProfileService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCatalog.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WorkflowProfileResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("List workflow profiles")]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var workflows = await workflowProfileService.GetAllAsync(cancellationToken);
        var response = workflows.Select(ToResponse).ToList();
        return Ok(ApiResponseFactory.Success<IReadOnlyList<WorkflowProfileResponse>>(response));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionCatalog.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Get workflow profile by id")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var workflow = await workflowProfileService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(ToResponse(workflow)));
    }

    [HttpPost]
    [Authorize(Policy = PermissionCatalog.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowProfileResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Create workflow profile")]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateWorkflowProfileRequest request,
        CancellationToken cancellationToken)
    {
        var created = await workflowProfileService.CreateAsync(
            new CreateWorkflowRequest(
                Name: request.Name,
                Description: request.Description,
                IsAutoRun: request.IsAutoRun ?? false,
                GraphJson: request.GraphJson),
            cancellationToken);

        return Created(
            $"/api/v1/system/workflows/{created.Id}",
            ApiResponseFactory.Success(ToResponse(created)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCatalog.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Update workflow profile")]
    public async Task<IActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateWorkflowProfileRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await workflowProfileService.UpdateAsync(
            id,
            new UpdateWorkflowRequest(
                Name: request.Name,
                Description: request.Description,
                IsAutoRun: request.IsAutoRun ?? false,
                GraphJson: request.GraphJson),
            cancellationToken);

        return Ok(ApiResponseFactory.Success(ToResponse(updated)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionCatalog.SettingsUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Delete workflow profile")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await workflowProfileService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/autorun")]
    [Authorize(Policy = PermissionCatalog.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<WorkflowProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Set workflow profile as auto-run")]
    public async Task<IActionResult> SetAutoRunAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var workflow = await workflowProfileService.SetAutoRunAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(ToResponse(workflow)));
    }

    private static WorkflowProfileResponse ToResponse(WorkflowProfileDto dto)
    {
        return new WorkflowProfileResponse(
            Id: dto.Id,
            Name: dto.Name,
            Description: dto.Description,
            IsAutoRun: dto.IsAutoRun,
            GraphJson: dto.GraphJson,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc);
    }
}

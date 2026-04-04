using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NekoHub.Api.Auth;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Ai.Commands;
using NekoHub.Application.Ai.Dtos;
using NekoHub.Application.Ai.Queries.Dtos;
using NekoHub.Application.Ai.Services;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/system/ai/providers")]
[Authorize(Policy = ApiKeyAuthorization.PolicyName)]
public sealed class AiProviderProfilesController(IAiProviderProfileService aiProviderProfileService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiProviderProfileResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("List AI provider profiles")]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var profiles = await aiProviderProfileService.ListAsync(cancellationToken);
        var response = ApiResponseFactory.Success<IReadOnlyList<AiProviderProfileResponse>>(
            profiles.Select(ToResponse).ToList());
        return Ok(response);
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<AiProviderProfileResponse?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Get active AI provider profile")]
    public async Task<IActionResult> GetActiveAsync(CancellationToken cancellationToken)
    {
        var profile = await aiProviderProfileService.GetActiveProfileAsync(cancellationToken);
        var response = ApiResponseFactory.Success(profile is null ? null : ToResponse(profile));
        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AiProviderProfileResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Create AI provider profile")]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateAiProviderProfileRequest request,
        CancellationToken cancellationToken)
    {
        var created = await aiProviderProfileService.CreateAsync(
            new CreateAiProviderProfileCommand(
                Name: request.Name,
                ApiBaseUrl: request.ApiBaseUrl,
                ApiKey: request.ApiKey,
                ModelName: request.ModelName,
                DefaultSystemPrompt: request.DefaultSystemPrompt,
                IsActive: request.IsActive),
            cancellationToken);

        var response = ApiResponseFactory.Success(ToResponse(created));
        return Created($"/api/v1/system/ai/providers/{created.Id}", response);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AiProviderProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Update AI provider profile")]
    public async Task<IActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateAiProviderProfileRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await aiProviderProfileService.UpdateAsync(
            new UpdateAiProviderProfileCommand(
                ProfileId: id,
                Name: request.Name,
                ApiBaseUrl: request.ApiBaseUrl,
                ApiKey: request.ApiKey,
                ModelName: request.ModelName,
                DefaultSystemPrompt: request.DefaultSystemPrompt,
                IsActive: request.IsActive),
            cancellationToken);

        var response = ApiResponseFactory.Success(ToResponse(updated));
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteAiProviderProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Delete AI provider profile")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var deleted = await aiProviderProfileService.DeleteAsync(id, cancellationToken);
        var response = ApiResponseFactory.Success(ToResponse(deleted));
        return Ok(response);
    }

    private static AiProviderProfileResponse ToResponse(AiProviderProfileQueryDto dto)
    {
        return new AiProviderProfileResponse(
            Id: dto.Id,
            Name: dto.Name,
            ApiBaseUrl: dto.ApiBaseUrl,
            ApiKey: dto.ApiKey,
            ModelName: dto.ModelName,
            DefaultSystemPrompt: dto.DefaultSystemPrompt,
            IsActive: dto.IsActive,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc);
    }

    private static DeleteAiProviderProfileResponse ToResponse(DeleteAiProviderProfileResultDto dto)
    {
        return new DeleteAiProviderProfileResponse(
            Id: dto.Id,
            WasActive: dto.WasActive,
            Status: dto.Status,
            DeletedAtUtc: dto.DeletedAtUtc);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NekoHub.Api.Auth;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Workflows.Services;
using NekoHub.Application.Auth;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
[Authorize(Policy = AuthorizationPolicies.ManagementAccess)]
public sealed class AssetOperationsController : ControllerBase
{
    [HttpGet("{id:guid}/derivatives/{kind}/content")]
    [Authorize(Policy = PermissionCatalog.AssetsRead)]
    public async Task<IActionResult> GetDerivativeContentAsync(Guid id, string kind,
        [FromServices] IAssetRepository assets, [FromServices] IAssetDerivativeRepository derivatives,
        [FromServices] IAssetStorageTargetSelector selector, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("asset_not_found", $"Asset '{id}' was not found.");
        var derivative = await derivatives.GetBySourceAndKindAsync(id, kind, cancellationToken)
            ?? throw new NotFoundException("asset_derivative_not_found", "Asset derivative was not found.");
        await using var storage = await selector.ResolveReadTargetAsync(asset.StorageProviderProfileId,
            derivative.StorageProvider, cancellationToken);
        var content = await storage.Storage.OpenReadAsync(derivative.StorageKey, cancellationToken)
            ?? throw new NotFoundException("asset_content_not_found", "Asset content was not found.");
        return File(content, derivative.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("workflows")]
    [Authorize(Policy = PermissionCatalog.AssetsUpdate)]
    public async Task<IActionResult> GetWorkflowsAsync([FromServices] IWorkflowProfileService workflows,
        CancellationToken cancellationToken)
    {
        var profiles = await workflows.GetAllAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(profiles.Select(x =>
            new AssetWorkflowOptionResponse(x.Id, x.Name, x.Description, x.IsAutoRun)).ToList()));
    }

    [HttpGet("storage-targets")]
    [Authorize(Policy = PermissionCatalog.AssetsCreate)]
    public async Task<IActionResult> GetStorageTargetsAsync(
        [FromServices] IStorageProviderProfileRepository repository,
        [FromServices] IStorageProviderProfileRuntimeFactory factory,
        [FromServices] IAssetStorageResolver resolver,
        CancellationToken cancellationToken)
    {
        var profiles = await repository.ListAsync(cancellationToken);
        var defaultProfile = await repository.GetDefaultAsync(cancellationToken);
        var result = new List<AssetStorageTargetResponse>();
        foreach (var profile in profiles.Where(x => x.IsEnabled))
        {
            try
            {
                await using var storage = factory.CreateStorageLease(profile);
                if (storage.Storage.SupportsWrite)
                {
                    result.Add(new AssetStorageTargetResponse(profile.Id, profile.Name, profile.DisplayName,
                        profile.ProviderType, defaultProfile?.Id == profile.Id));
                }
            }
            catch (InvalidOperationException)
            {
                // 仅返回当前运行时能实际写入的目标，不暴露配置及凭据。
            }
        }
        if (defaultProfile is null)
        {
            var storage = resolver.ResolveDefault();
            if (storage.SupportsWrite)
            {
                result.Insert(0, new AssetStorageTargetResponse(null, storage.ProviderName, null, storage.ProviderType, true));
            }
        }
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:guid}/jobs")]
    [Authorize(Policy = PermissionCatalog.AssetsRead)]
    public async Task<IActionResult> GetJobsAsync(Guid id, [FromServices] IAssetProcessingJobService jobs,
        CancellationToken cancellationToken)
    {
        return Ok(ApiResponseFactory.Success(await jobs.ListAsync(id, cancellationToken)));
    }

    [HttpPost("{id:guid}/jobs/{jobId:guid}/retry")]
    [Authorize(Policy = PermissionCatalog.AssetsUpdate)]
    public async Task<IActionResult> RetryJobAsync(Guid id, Guid jobId, [FromServices] IAssetProcessingJobService jobs,
        CancellationToken cancellationToken)
    {
        await jobs.RetryAsync(id, jobId, cancellationToken);
        return NoContent();
    }
}

public sealed record AssetStorageTargetResponse(Guid? Id, string Name, string? DisplayName, string ProviderType, bool IsDefault);
public sealed record AssetWorkflowOptionResponse(Guid Id, string Name, string? Description, bool IsAutoRun);

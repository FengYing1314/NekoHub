using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NekoHub.Api.Auth;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Storage.Commands;
using NekoHub.Application.Storage.Dtos;
using NekoHub.Application.Storage.Queries.Dtos;
using NekoHub.Application.Storage.Services;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/system/storage")]
[Authorize(Policy = ApiKeyAuthorization.PolicyName)]
public sealed class SystemStorageController(
    IStorageProviderQueryService storageProviderQueryService,
    IStorageProviderProfileManagementService storageProviderProfileManagementService,
    IGitHubRepoProfileAccessService gitHubRepoProfileAccessService) : ControllerBase
{
    [HttpGet("providers")]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderOverviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Get storage provider profiles and runtime summary")]
    public async Task<IActionResult> GetProvidersAsync(CancellationToken cancellationToken)
    {
        var overview = await storageProviderQueryService.GetOverviewAsync(cancellationToken);
        var response = ApiResponseFactory.Success(ToResponse(overview));
        return Ok(response);
    }

    [HttpPost("providers")]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderProfileResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Create storage provider profile")]
    public async Task<IActionResult> CreateProviderAsync(
        [FromBody] CreateStorageProviderProfileRequest request,
        CancellationToken cancellationToken)
    {
        var created = await storageProviderProfileManagementService.CreateAsync(
            new CreateStorageProviderProfileCommand(
                Name: request.Name,
                DisplayName: request.DisplayName,
                ProviderType: request.ProviderType,
                IsEnabled: request.IsEnabled ?? true,
                IsDefault: request.IsDefault ?? false,
                Configuration: request.Configuration,
                SecretConfiguration: request.SecretConfiguration),
            cancellationToken);

        var response = ApiResponseFactory.Success(ToResponse(created));
        return Created($"/api/v1/system/storage/providers/{created.Id}", response);
    }

    [HttpPatch("providers/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Update storage provider profile")]
    public async Task<IActionResult> UpdateProviderAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateStorageProviderProfileRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await storageProviderProfileManagementService.UpdateAsync(
            new UpdateStorageProviderProfileCommand(
                ProfileId: id,
                Name: request.Name,
                DisplayName: request.DisplayName,
                IsEnabled: request.IsEnabled,
                Configuration: request.Configuration,
                SecretConfiguration: request.SecretConfiguration),
            cancellationToken);

        var response = ApiResponseFactory.Success(ToResponse(updated));
        return Ok(response);
    }

    [HttpDelete("providers/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteStorageProviderProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Delete storage provider profile")]
    public async Task<IActionResult> DeleteProviderAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var deleted = await storageProviderProfileManagementService.DeleteAsync(id, cancellationToken);
        var response = ApiResponseFactory.Success(ToResponse(deleted));
        return Ok(response);
    }

    [HttpPost("providers/{id:guid}/set-default")]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Set storage provider profile as default in database")]
    public async Task<IActionResult> SetDefaultProviderAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var updated = await storageProviderProfileManagementService.SetDefaultAsync(id, cancellationToken);
        var response = ApiResponseFactory.Success(ToResponse(updated));
        return Ok(response);
    }

    [HttpGet("providers/{id:guid}/github-repo/browse")]
    [ProducesResponseType(typeof(ApiResponse<GitHubRepoBrowseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Browse github-repo directory by storage provider profile id")]
    public async Task<IActionResult> BrowseGitHubRepoAsync(
        [FromRoute] Guid id,
        [FromQuery] BrowseGitHubRepoStorageProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await gitHubRepoProfileAccessService.BrowseAsync(
            id,
            new GitHubRepoBrowseProfileRequestDto(
                Path: request.Path,
                Recursive: request.Recursive,
                MaxDepth: request.MaxDepth,
                Type: request.Type,
                Keyword: request.Keyword,
                Page: request.Page,
                PageSize: request.PageSize),
            cancellationToken);

        var response = ApiResponseFactory.Success(ToResponse(result));
        return Ok(response);
    }

    [HttpPost("providers/{id:guid}/github-repo/upsert")]
    [ProducesResponseType(typeof(ApiResponse<GitHubRepoUpsertResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Upsert single github-repo file by storage provider profile id")]
    public async Task<IActionResult> UpsertGitHubRepoAsync(
        [FromRoute] Guid id,
        [FromBody] UpsertGitHubRepoStorageProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await gitHubRepoProfileAccessService.UpsertAsync(
            id,
            new GitHubRepoUpsertProfileRequestDto(
                Path: request.Path,
                ContentBase64: request.ContentBase64,
                CommitMessage: request.CommitMessage,
                ExpectedSha: request.ExpectedSha),
            cancellationToken);

        var response = ApiResponseFactory.Success(ToResponse(result));
        return Ok(response);
    }

    private static StorageProviderOverviewResponse ToResponse(StorageProviderOverviewQueryDto dto)
    {
        return new StorageProviderOverviewResponse(
            Profiles: dto.Profiles.Select(ToResponse).ToList(),
            DefaultProfile: dto.DefaultProfile is null ? null : ToResponse(dto.DefaultProfile),
            DefaultWriteProfile: dto.DefaultWriteProfile is null ? null : ToResponse(dto.DefaultWriteProfile),
            Runtime: ToResponse(dto.Runtime),
            Alignment: ToResponse(dto.Alignment));
    }

    private static StorageProviderProfileResponse ToResponse(StorageProviderProfileQueryDto dto)
    {
        return new StorageProviderProfileResponse(
            Id: dto.Id,
            Name: dto.Name,
            DisplayName: dto.DisplayName,
            ProviderType: dto.ProviderType,
            IsEnabled: dto.IsEnabled,
            IsDefault: dto.IsDefault,
            Capabilities: ToResponse(dto.Capabilities),
            ConfigurationSummary: new StorageProviderConfigurationSummaryResponse(
                ProviderName: dto.ConfigurationSummary.ProviderName,
                RootPath: dto.ConfigurationSummary.RootPath,
                EndpointHost: dto.ConfigurationSummary.EndpointHost,
                BucketOrContainer: dto.ConfigurationSummary.BucketOrContainer,
                Region: dto.ConfigurationSummary.Region,
                PublicBaseUrl: dto.ConfigurationSummary.PublicBaseUrl,
                ForcePathStyle: dto.ConfigurationSummary.ForcePathStyle,
                Owner: dto.ConfigurationSummary.Owner,
                Repository: dto.ConfigurationSummary.Repository,
                Reference: dto.ConfigurationSummary.Reference,
                ReleaseTagMode: dto.ConfigurationSummary.ReleaseTagMode,
                FixedTag: dto.ConfigurationSummary.FixedTag,
                PathPrefix: dto.ConfigurationSummary.PathPrefix,
                VisibilityPolicy: dto.ConfigurationSummary.VisibilityPolicy,
                BasePath: dto.ConfigurationSummary.BasePath,
                AssetPathPrefix: dto.ConfigurationSummary.AssetPathPrefix,
                ApiBaseUrl: dto.ConfigurationSummary.ApiBaseUrl,
                RawBaseUrl: dto.ConfigurationSummary.RawBaseUrl),
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc);
    }

    private static DeleteStorageProviderProfileResponse ToResponse(DeleteStorageProviderProfileResultDto dto)
    {
        return new DeleteStorageProviderProfileResponse(
            Id: dto.Id,
            WasDefault: dto.WasDefault,
            Status: dto.Status,
            DeletedAtUtc: dto.DeletedAtUtc);
    }

    private static StorageRuntimeSummaryResponse ToResponse(StorageRuntimeSummaryQueryDto dto)
    {
        return new StorageRuntimeSummaryResponse(
            ProviderType: dto.ProviderType,
            ProviderName: dto.ProviderName,
            Capabilities: ToResponse(dto.Capabilities),
            IsConfigurationDriven: dto.IsConfigurationDriven,
            MatchesDefaultProfileType: dto.MatchesDefaultProfileType);
    }

    private static StorageProviderCapabilitiesResponse ToResponse(StorageProviderCapabilitiesQueryDto dto)
    {
        return new StorageProviderCapabilitiesResponse(
            SupportsPublicRead: dto.SupportsPublicRead,
            SupportsPrivateRead: dto.SupportsPrivateRead,
            SupportsVisibilityToggle: dto.SupportsVisibilityToggle,
            SupportsDelete: dto.SupportsDelete,
            SupportsDirectPublicUrl: dto.SupportsDirectPublicUrl,
            RequiresAccessProxy: dto.RequiresAccessProxy,
            RecommendedForPrimaryStorage: dto.RecommendedForPrimaryStorage,
            IsPlatformBacked: dto.IsPlatformBacked,
            IsExperimental: dto.IsExperimental,
            RequiresTokenForPrivateRead: dto.RequiresTokenForPrivateRead);
    }

    private static StorageRuntimeAlignmentStatusResponse ToResponse(StorageRuntimeAlignmentStatusQueryDto dto)
    {
        return new StorageRuntimeAlignmentStatusResponse(
            RuntimeSelectionSource: dto.RuntimeSelectionSource,
            HasDefaultProfile: dto.HasDefaultProfile,
            IsDefaultProfileEnabled: dto.IsDefaultProfileEnabled,
            ProviderTypeMatchesDefaultProfile: dto.ProviderTypeMatchesDefaultProfile,
            Code: dto.Code,
            Message: dto.Message);
    }

    private static GitHubRepoBrowseResponse ToResponse(GitHubRepoBrowseProfileResultDto dto)
    {
        return new GitHubRepoBrowseResponse(
            ProfileId: dto.ProfileId,
            RequestedPath: dto.RequestedPath,
            Recursive: dto.Recursive,
            MaxDepth: dto.MaxDepth,
            Type: dto.Type,
            Keyword: dto.Keyword,
            Total: dto.Total,
            Page: dto.Page,
            PageSize: dto.PageSize,
            HasMore: dto.HasMore,
            VisibilityPolicy: dto.VisibilityPolicy,
            UsesControlledRead: dto.UsesControlledRead,
            Items: dto.Items.Select(ToResponse).ToList());
    }

    private static GitHubRepoBrowseItemResponse ToResponse(GitHubRepoBrowseProfileEntryDto dto)
    {
        return new GitHubRepoBrowseItemResponse(
            Name: dto.Name,
            Path: dto.Path,
            Type: dto.Type,
            IsDirectory: dto.IsDirectory,
            IsFile: dto.IsFile,
            Size: dto.Size,
            Sha: dto.Sha,
            PublicUrl: dto.PublicUrl);
    }

    private static GitHubRepoUpsertResponse ToResponse(GitHubRepoUpsertProfileResultDto dto)
    {
        return new GitHubRepoUpsertResponse(
            ProfileId: dto.ProfileId,
            Path: dto.Path,
            Operation: dto.Operation,
            Size: dto.Size,
            Sha: dto.Sha,
            VisibilityPolicy: dto.VisibilityPolicy,
            UsesControlledRead: dto.UsesControlledRead,
            PublicUrl: dto.PublicUrl);
    }
}

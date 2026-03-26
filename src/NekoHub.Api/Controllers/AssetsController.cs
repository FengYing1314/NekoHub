using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NekoHub.Api.Auth;
using NekoHub.Api.Configuration;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Assets.Commands;
using NekoHub.Application.Assets.Dtos;
using NekoHub.Application.Assets.Queries;
using NekoHub.Application.Assets.Queries.Dtos;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Assets;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
[Authorize(Policy = ApiKeyAuthorization.PolicyName)]
public sealed class AssetsController(
    IAssetCommandService assetCommandService,
    IAssetQueryService assetQueryService,
    IAssetContentService assetContentService,
    IOptions<AssetApiOptions> assetApiOptions) : ControllerBase
{
    private readonly AssetApiOptions _assetApiOptions = assetApiOptions.Value;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Upload image asset")]
    [EndpointDescription("""
                         上传图片资产，支持 multipart/form-data。
                         Swagger 请求示例：
                         - file: kitten.png
                         - description: "cat avatar"
                         - altText: "orange cat looking at camera"
                         - isPublic: true
                         
                         成功响应示例（ApiResponse）：
                         {
                           "data": {
                             "id": "01956f8d-88e4-7c6a-a8f1-5f235293db7a",
                             "type": "image",
                             "status": "ready",
                             "originalFileName": "kitten.png",
                             "contentType": "image/png",
                             "size": 183204,
                             "storageProvider": "local",
                             "storageKey": "2026/03/10/...",
                             "publicUrl": "https://localhost:7151/content/...",
                             "isPublic": true
                           }
                         }
                         """)]
    public async Task<IActionResult> UploadAsync([FromForm] UploadAssetFormRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            throw new ValidationException("asset_file_required", "The form field 'file' is required.");
        }

        if (request.Description is { Length: > 1000 })
        {
            throw new ValidationException("asset_description_too_long", "Description must be 1000 characters or fewer.");
        }

        if (request.AltText is { Length: > 1000 })
        {
            throw new ValidationException("asset_alt_text_too_long", "Alt text must be 1000 characters or fewer.");
        }

        if (request.File.Length <= 0)
        {
            throw new ValidationException("asset_file_empty", "Uploaded file is empty.");
        }

        if (request.File.Length > _assetApiOptions.MaxUploadSizeBytes)
        {
            throw new ValidationException(
                "asset_file_too_large",
                $"File size exceeds limit {_assetApiOptions.MaxUploadSizeBytes} bytes.");
        }

        if (string.IsNullOrWhiteSpace(request.File.ContentType))
        {
            throw new ValidationException("asset_content_type_missing", "Content type is required.");
        }

        var isAllowedType = _assetApiOptions.AllowedContentTypes
            .Contains(request.File.ContentType, StringComparer.OrdinalIgnoreCase);
        if (!isAllowedType)
        {
            throw new ValidationException("asset_content_type_not_allowed", $"Content type '{request.File.ContentType}' is not allowed.");
        }

        await using var fileStream = request.File.OpenReadStream();
        var uploaded = await assetCommandService.UploadAsync(
            new UploadAssetCommand(
                Content: fileStream,
                OriginalFileName: request.File.FileName,
                DeclaredContentType: request.File.ContentType,
                DeclaredSize: request.File.Length,
                Description: request.Description,
                AltText: request.AltText,
                IsPublic: request.IsPublic ?? true),
            cancellationToken);

        var response = ApiResponseFactory.Success(ToResponse(uploaded));
        return Created($"/api/v1/assets/{uploaded.Id}", response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Get asset details by id")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var asset = await assetQueryService.GetByIdAsync(id, cancellationToken);
        var response = ApiResponseFactory.Success(ToResponse(asset));
        return Ok(response);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AssetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Patch asset metadata")]
    public async Task<IActionResult> PatchAsync(
        [FromRoute] Guid id,
        [FromBody] PatchAssetMetadataRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Description.IsSet && request.Description.Value is { Length: > 1000 })
        {
            throw new ValidationException("asset_description_too_long", "Description must be 1000 characters or fewer.");
        }

        if (request.AltText.IsSet && request.AltText.Value is { Length: > 1000 })
        {
            throw new ValidationException("asset_alt_text_too_long", "Alt text must be 1000 characters or fewer.");
        }

        await assetCommandService.PatchAsync(
            new PatchAssetMetadataCommand(
                AssetId: id,
                Description: request.Description,
                AltText: request.AltText,
                OriginalFileName: request.OriginalFileName,
                IsPublic: request.IsPublic),
            cancellationToken);

        var asset = await assetQueryService.GetByIdAsync(id, cancellationToken);
        var response = ApiResponseFactory.Success(ToResponse(asset));
        return Ok(response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AssetPagedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("List assets with pagination")]
    public async Task<IActionResult> GetPagedAsync(
        [FromQuery] GetAssetsPagedRequest? request,
        CancellationToken cancellationToken)
    {
        var resolvedPage = ResolvePage(request?.Page);
        var resolvedPageSize = ResolvePageSize(request?.PageSize);
        var resolvedSortBy = ResolveSortBy(request?.OrderBy ?? request?.SortBy);
        var resolvedSortDirection = ResolveSortDirection(request?.OrderDirection ?? request?.SortDirection);

        var query = new GetAssetsPagedQuery(
            Page: resolvedPage,
            PageSize: resolvedPageSize,
            MaxPageSize: _assetApiOptions.MaxPageSize,
            Query: request?.Query ?? request?.Keyword,
            ContentType: request?.ContentType,
            Status: ResolveStatus(request?.Status),
            SortBy: resolvedSortBy,
            SortDirection: resolvedSortDirection);

        var paged = await assetQueryService.GetPagedAsync(query, cancellationToken);
        var response = ApiResponseFactory.Success(ToPagedResponse(paged));
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteAssetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Delete asset by id")]
    [EndpointDescription("""
                         硬删除资产：删除资产记录并删除对应存储文件。
                         若资产不存在，返回 404，error.code = asset_not_found。
                         """)]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var deleted = await assetCommandService.DeleteAsync(new DeleteAssetCommand(id), cancellationToken);
        var response = ApiResponseFactory.Success(ToResponse(deleted));
        return Ok(response);
    }

    [HttpPost("batch-delete")]
    [ProducesResponseType(typeof(ApiResponse<BatchDeleteAssetsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Batch delete assets")]
    public async Task<IActionResult> BatchDeleteAsync([FromBody] Guid[]? ids, CancellationToken cancellationToken)
    {
        var deleted = await assetCommandService.BatchDeleteAsync(
            new BatchDeleteAssetsCommand(ids ?? []),
            cancellationToken);
        var response = ApiResponseFactory.Success(ToResponse(deleted));
        return Ok(response);
    }

    [HttpGet("usage-stats")]
    [ProducesResponseType(typeof(ApiResponse<AssetUsageStatsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Get asset usage stats")]
    public async Task<IActionResult> GetUsageStatsAsync(CancellationToken cancellationToken)
    {
        var stats = await assetQueryService.GetUsageStatsAsync(cancellationToken);
        var response = ApiResponseFactory.Success(ToResponse(stats));
        return Ok(response);
    }

    [HttpGet("{id:guid}/content")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Redirect to public content URL")]
    [EndpointDescription("""
                         第一版内容访问采用重定向语义：
                         - 公开资产：返回 307，并在 Location 头中提供 publicUrl
                         - 私有资产 / 不存在资产：统一返回 404 + error.code = asset_not_found
                         - 失败：统一返回 404 + error.code = asset_not_found
                         """)]
    public async Task<IActionResult> GetContentAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var redirect = await assetContentService.GetRedirectAsync(id, cancellationToken);
        return redirect.PreserveMethod
            ? RedirectPreserveMethod(redirect.RedirectUrl)
            : Redirect(redirect.RedirectUrl);
    }

    private static AssetResponse ToResponse(AssetDto dto)
    {
        return new AssetResponse(
            Id: dto.Id,
            Type: dto.Type,
            Status: dto.Status,
            OriginalFileName: dto.OriginalFileName,
            StoredFileName: dto.StoredFileName,
            ContentType: dto.ContentType,
            Extension: dto.Extension,
            Size: dto.Size,
            Width: dto.Width,
            Height: dto.Height,
            ChecksumSha256: dto.ChecksumSha256,
            StorageProvider: dto.StorageProvider,
            StorageKey: dto.StorageKey,
            PublicUrl: dto.PublicUrl,
            IsPublic: dto.IsPublic,
            Description: dto.Description,
            AltText: dto.AltText,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc,
            Derivatives: [],
            StructuredResults: [],
            LatestExecutionSummary: null);
    }

    private static AssetResponse ToResponse(AssetDetailsQueryDto dto)
    {
        return new AssetResponse(
            Id: dto.Id,
            Type: dto.Type,
            Status: dto.Status,
            OriginalFileName: dto.OriginalFileName,
            StoredFileName: dto.StoredFileName,
            ContentType: dto.ContentType,
            Extension: dto.Extension,
            Size: dto.Size,
            Width: dto.Width,
            Height: dto.Height,
            ChecksumSha256: dto.ChecksumSha256,
            StorageProvider: dto.StorageProvider,
            StorageKey: dto.StorageKey,
            PublicUrl: dto.PublicUrl,
            IsPublic: dto.IsPublic,
            Description: dto.Description,
            AltText: dto.AltText,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc,
            Derivatives: dto.Derivatives
                .Select(derivative => new AssetDerivativeSummaryResponse(
                    Kind: derivative.Kind,
                    ContentType: derivative.ContentType,
                    Extension: derivative.Extension,
                    Size: derivative.Size,
                    Width: derivative.Width,
                    Height: derivative.Height,
                    PublicUrl: derivative.PublicUrl,
                    CreatedAtUtc: derivative.CreatedAtUtc))
                .ToList(),
            StructuredResults: dto.StructuredResults
                .Select(result => new AssetStructuredResultSummaryResponse(
                    Kind: result.Kind,
                    PayloadJson: result.PayloadJson,
                    CreatedAtUtc: result.CreatedAtUtc))
                .ToList(),
            LatestExecutionSummary: dto.LatestExecutionSummary is null
                ? null
                : new AssetLatestExecutionSummaryResponse(
                    ExecutionId: dto.LatestExecutionSummary.ExecutionId,
                    SkillName: dto.LatestExecutionSummary.SkillName,
                    TriggerSource: dto.LatestExecutionSummary.TriggerSource,
                    StartedAtUtc: dto.LatestExecutionSummary.StartedAtUtc,
                    CompletedAtUtc: dto.LatestExecutionSummary.CompletedAtUtc,
                    Succeeded: dto.LatestExecutionSummary.Succeeded,
                    Steps: dto.LatestExecutionSummary.Steps
                        .Select(step => new AssetLatestExecutionStepSummaryResponse(
                            StepName: step.StepName,
                            Succeeded: step.Succeeded,
                            ErrorMessage: step.ErrorMessage,
                            StartedAtUtc: step.StartedAtUtc,
                            CompletedAtUtc: step.CompletedAtUtc))
                        .ToList()));
    }

    private static AssetPagedResponse ToPagedResponse(AssetPagedQueryDto dto)
    {
        var items = dto.Items
            .Select(item => new AssetListItemResponse(
                Id: item.Id,
                Type: item.Type,
                Status: item.Status,
                OriginalFileName: item.OriginalFileName,
                ContentType: item.ContentType,
                Size: item.Size,
                Width: item.Width,
                Height: item.Height,
                StorageProvider: item.StorageProvider,
                PublicUrl: item.PublicUrl,
                IsPublic: item.IsPublic,
                CreatedAtUtc: item.CreatedAtUtc,
                UpdatedAtUtc: item.UpdatedAtUtc))
            .ToList();

        return new AssetPagedResponse(
            Items: items,
            Page: dto.Page,
            PageSize: dto.PageSize,
            Total: dto.Total);
    }

    private static DeleteAssetResponse ToResponse(DeleteAssetResultDto dto)
    {
        return new DeleteAssetResponse(
            Id: dto.Id,
            Status: dto.Status,
            DeletedAtUtc: dto.DeletedAtUtc);
    }

    private static BatchDeleteAssetsResponse ToResponse(BatchDeleteAssetsResultDto dto)
    {
        return new BatchDeleteAssetsResponse(
            RequestedCount: dto.RequestedCount,
            DeletedCount: dto.DeletedCount,
            NotFoundIds: dto.NotFoundIds);
    }

    private static AssetUsageStatsResponse ToResponse(AssetUsageStatsQueryDto dto)
    {
        return new AssetUsageStatsResponse(
            TotalAssets: dto.TotalAssets,
            TotalBytes: dto.TotalBytes,
            TotalDerivatives: dto.TotalDerivatives,
            ContentTypeBreakdown: dto.ContentTypeBreakdown
                .Select(static item => new AssetContentTypeBreakdownResponse(
                    ContentType: item.ContentType,
                    Count: item.Count,
                    TotalBytes: item.TotalBytes))
                .ToList(),
            MostActiveSkill: dto.MostActiveSkill is null
                ? null
                : new AssetSkillUsageSummaryResponse(
                    SkillName: dto.MostActiveSkill.SkillName,
                    RunCount: dto.MostActiveSkill.RunCount));
    }

    private int ResolvePage(int? page)
    {
        if (!page.HasValue || page.Value < 1)
        {
            return 1;
        }

        return page.Value;
    }

    private int ResolvePageSize(int? pageSize)
    {
        if (!pageSize.HasValue || pageSize.Value < 1)
        {
            return _assetApiOptions.DefaultPageSize;
        }

        return Math.Min(pageSize.Value, _assetApiOptions.MaxPageSize);
    }

    private static AssetListSortBy ResolveSortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return AssetListSortBy.CreatedAtUtc;
        }

        if (sortBy.Equals("size", StringComparison.OrdinalIgnoreCase))
        {
            return AssetListSortBy.Size;
        }

        if (sortBy.Equals("createdAt", StringComparison.OrdinalIgnoreCase)
            || sortBy.Equals("createdAtUtc", StringComparison.OrdinalIgnoreCase))
        {
            return AssetListSortBy.CreatedAtUtc;
        }

        return AssetListSortBy.CreatedAtUtc;
    }

    private static AssetListSortDirection ResolveSortDirection(string? sortDirection)
    {
        if (string.IsNullOrWhiteSpace(sortDirection))
        {
            return AssetListSortDirection.Desc;
        }

        if (sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase))
        {
            return AssetListSortDirection.Asc;
        }

        if (sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            return AssetListSortDirection.Desc;
        }

        return AssetListSortDirection.Desc;
    }

    private static AssetStatus? ResolveStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return Enum.TryParse<AssetStatus>(status.Trim(), ignoreCase: true, out var parsedStatus)
            ? parsedStatus
            : null;
    }
}

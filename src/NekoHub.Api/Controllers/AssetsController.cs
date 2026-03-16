using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NekoHub.Api.Configuration;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Assets.Commands;
using NekoHub.Application.Assets.Dtos;
using NekoHub.Application.Assets.Queries;
using NekoHub.Application.Assets.Queries.Dtos;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
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
                             "publicUrl": "https://localhost:7151/content/..."
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
                AltText: request.AltText),
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

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AssetPagedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("List assets with pagination")]
    public async Task<IActionResult> GetPagedAsync(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        // 当查询参数缺失时，使用配置里的默认分页值，避免 controller 硬编码常量。
        var resolvedPage = page ?? 1;
        var resolvedPageSize = pageSize ?? _assetApiOptions.DefaultPageSize;

        var query = new GetAssetsPagedQuery(
            Page: resolvedPage,
            PageSize: resolvedPageSize,
            MaxPageSize: _assetApiOptions.MaxPageSize);

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

    [HttpGet("{id:guid}/content")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Redirect to public content URL")]
    [EndpointDescription("""
                         第一版内容访问采用重定向语义：
                         - 成功：返回 307，并在 Location 头中提供 publicUrl
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
            Description: dto.Description,
            AltText: dto.AltText,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc,
            Derivatives: [],
            StructuredResults: []);
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
                .ToList());
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
}

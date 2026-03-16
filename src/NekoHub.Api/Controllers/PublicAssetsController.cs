using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NekoHub.Api.Configuration;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Assets.Queries;
using NekoHub.Application.Assets.Queries.Dtos;
using NekoHub.Application.Assets.Services;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/public/assets")]
[AllowAnonymous]
public sealed class PublicAssetsController(
    IAssetQueryService assetQueryService,
    IOptions<AssetApiOptions> assetApiOptions) : ControllerBase
{
    private readonly AssetApiOptions _assetApiOptions = assetApiOptions.Value;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PublicAssetPagedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [EndpointSummary("List public ready assets")]
    public async Task<IActionResult> GetPagedAsync(
        [FromQuery] GetPublicAssetsPagedRequest? request,
        CancellationToken cancellationToken)
    {
        var paged = await assetQueryService.GetPublicPagedAsync(
            new GetAssetsPagedQuery(
                Page: ResolvePage(request?.Page),
                PageSize: ResolvePageSize(request?.PageSize),
                MaxPageSize: _assetApiOptions.MaxPageSize,
                Query: request?.Query,
                ContentType: request?.ContentType,
                Status: null,
                SortBy: AssetListSortBy.CreatedAtUtc,
                SortDirection: AssetListSortDirection.Desc),
            cancellationToken);

        return Ok(ApiResponseFactory.Success(ToResponse(paged)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PublicAssetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get public asset detail by id")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var asset = await assetQueryService.GetPublicByIdAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(ToResponse(asset)));
    }

    private int ResolvePage(int? page)
    {
        return page is > 0 ? page.Value : 1;
    }

    private int ResolvePageSize(int? pageSize)
    {
        if (pageSize is null or <= 0)
        {
            return _assetApiOptions.DefaultPageSize;
        }

        return Math.Min(pageSize.Value, _assetApiOptions.MaxPageSize);
    }

    private static PublicAssetPagedResponse ToResponse(PublicAssetPagedQueryDto dto)
    {
        return new PublicAssetPagedResponse(
            Items: dto.Items.Select(ToResponse).ToList(),
            Page: dto.Page,
            PageSize: dto.PageSize,
            Total: dto.Total);
    }

    private static PublicAssetListItemResponse ToResponse(PublicAssetListItemQueryDto dto)
    {
        return new PublicAssetListItemResponse(
            Id: dto.Id,
            Type: dto.Type,
            OriginalFileName: dto.OriginalFileName,
            ContentType: dto.ContentType,
            Size: dto.Size,
            Width: dto.Width,
            Height: dto.Height,
            PublicUrl: dto.PublicUrl,
            Description: dto.Description,
            AltText: dto.AltText,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc);
    }

    private static PublicAssetResponse ToResponse(PublicAssetQueryDto dto)
    {
        return new PublicAssetResponse(
            Id: dto.Id,
            Type: dto.Type,
            OriginalFileName: dto.OriginalFileName,
            ContentType: dto.ContentType,
            Extension: dto.Extension,
            Size: dto.Size,
            Width: dto.Width,
            Height: dto.Height,
            PublicUrl: dto.PublicUrl,
            Description: dto.Description,
            AltText: dto.AltText,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc,
            Derivatives: dto.Derivatives
                .Select(static derivative => new PublicAssetDerivativeSummaryResponse(
                    Kind: derivative.Kind,
                    ContentType: derivative.ContentType,
                    Extension: derivative.Extension,
                    Size: derivative.Size,
                    Width: derivative.Width,
                    Height: derivative.Height,
                    PublicUrl: derivative.PublicUrl,
                    CreatedAtUtc: derivative.CreatedAtUtc))
                .ToList());
    }
}

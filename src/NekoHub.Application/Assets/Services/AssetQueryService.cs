using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Assets.Queries;
using NekoHub.Application.Assets.Queries.Dtos;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Assets;
using NekoHub.Domain.Skills;

namespace NekoHub.Application.Assets.Services;

public sealed class AssetQueryService(
    IAssetRepository assetRepository,
    IAssetDerivativeRepository assetDerivativeRepository,
    IAssetStructuredResultRepository assetStructuredResultRepository,
    IAssetSkillExecutionRepository assetSkillExecutionRepository) : IAssetQueryService
{
    public async Task<AssetDetailsQueryDto> GetByIdAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var asset = await assetRepository.GetByIdAsync(assetId, cancellationToken);
        if (asset is null)
        {
            throw new NotFoundException("asset_not_found", $"Asset '{assetId}' was not found.");
        }

        var derivatives = await assetDerivativeRepository.GetBySourceAssetIdAsync(asset.Id, cancellationToken);
        var structuredResults = await assetStructuredResultRepository.GetBySourceAssetIdAsync(asset.Id, cancellationToken);
        var latestExecution = await assetSkillExecutionRepository.GetLatestBySourceAssetIdAsync(asset.Id, cancellationToken);
        var latestExecutionSteps = latestExecution is null
            ? []
            : await assetSkillExecutionRepository.GetByExecutionIdAsync(latestExecution.Id, cancellationToken);

        return ToDetailsDto(asset, derivatives, structuredResults, latestExecution, latestExecutionSteps);
    }

    public async Task<AssetPagedQueryDto> GetPagedAsync(GetAssetsPagedQuery query, CancellationToken cancellationToken = default)
    {
        ValidatePaging(query);

        var normalizedQuery = NormalizeQuery(query);
        var paged = await assetRepository.GetPagedAsync(normalizedQuery, cancellationToken);
        var items = paged.Items.Select(ToListItemDto).ToList();

        return new AssetPagedQueryDto(items, paged.Page, paged.PageSize, paged.Total);
    }

    public Task<AssetUsageStatsQueryDto> GetUsageStatsAsync(CancellationToken cancellationToken = default)
    {
        return assetRepository.GetUsageStatsAsync(cancellationToken);
    }

    private static void ValidatePaging(GetAssetsPagedQuery query)
    {
        if (query.Page < 1)
        {
            throw new ValidationException("page_out_of_range", "Page must be greater than or equal to 1.");
        }

        if (query.PageSize < 1)
        {
            throw new ValidationException("page_size_out_of_range", "PageSize must be greater than or equal to 1.");
        }

        if (query.PageSize > query.MaxPageSize)
        {
            throw new ValidationException(
                "page_size_too_large",
                $"PageSize must be less than or equal to {query.MaxPageSize}.");
        }
    }

    private static GetAssetsPagedQuery NormalizeQuery(GetAssetsPagedQuery query)
    {
        var keyword = string.IsNullOrWhiteSpace(query.Query)
            ? null
            : query.Query.Trim();

        var contentType = string.IsNullOrWhiteSpace(query.ContentType)
            ? null
            : query.ContentType.Trim();

        return query with
        {
            Query = keyword,
            ContentType = contentType
        };
    }

    private static AssetDetailsQueryDto ToDetailsDto(
        Asset asset,
        IReadOnlyList<AssetDerivative> derivatives,
        IReadOnlyList<AssetStructuredResult> structuredResults,
        SkillExecution? latestExecution,
        IReadOnlyList<SkillExecutionStepResult> latestExecutionSteps)
    {
        return new AssetDetailsQueryDto(
            Id: asset.Id,
            Type: asset.Type,
            Status: asset.Status,
            OriginalFileName: asset.OriginalFileName,
            StoredFileName: asset.StoredFileName,
            ContentType: asset.ContentType,
            Extension: asset.Extension,
            Size: asset.Size,
            Width: asset.Width,
            Height: asset.Height,
            ChecksumSha256: asset.ChecksumSha256,
            StorageProvider: asset.StorageProvider,
            StorageKey: asset.StorageKey,
            PublicUrl: asset.PublicUrl,
            Description: asset.Description,
            AltText: asset.AltText,
            CreatedAtUtc: asset.CreatedAtUtc,
            UpdatedAtUtc: asset.UpdatedAtUtc,
            Derivatives: derivatives
                .OrderBy(static derivative => derivative.CreatedAtUtc)
                .Select(ToDerivativeDto)
                .ToList(),
            StructuredResults: structuredResults
                .OrderBy(static result => result.CreatedAtUtc)
                .Select(ToStructuredResultDto)
                .ToList(),
            LatestExecutionSummary: latestExecution is null
                ? null
                : ToLatestExecutionSummaryDto(latestExecution, latestExecutionSteps));
    }

    private static AssetDerivativeSummaryQueryDto ToDerivativeDto(AssetDerivative derivative)
    {
        return new AssetDerivativeSummaryQueryDto(
            Kind: derivative.Kind,
            ContentType: derivative.ContentType,
            Extension: derivative.Extension,
            Size: derivative.Size,
            Width: derivative.Width,
            Height: derivative.Height,
            PublicUrl: derivative.PublicUrl,
            CreatedAtUtc: derivative.CreatedAtUtc);
    }

    private static AssetStructuredResultSummaryQueryDto ToStructuredResultDto(AssetStructuredResult result)
    {
        return new AssetStructuredResultSummaryQueryDto(
            Kind: result.Kind,
            PayloadJson: result.PayloadJson,
            CreatedAtUtc: result.CreatedAtUtc);
    }

    private static AssetListItemQueryDto ToListItemDto(Asset asset)
    {
        return new AssetListItemQueryDto(
            Id: asset.Id,
            Type: asset.Type,
            Status: asset.Status,
            OriginalFileName: asset.OriginalFileName,
            ContentType: asset.ContentType,
            Size: asset.Size,
            Width: asset.Width,
            Height: asset.Height,
            StorageProvider: asset.StorageProvider,
            PublicUrl: asset.PublicUrl,
            CreatedAtUtc: asset.CreatedAtUtc,
            UpdatedAtUtc: asset.UpdatedAtUtc);
    }

    private static AssetLatestExecutionSummaryQueryDto ToLatestExecutionSummaryDto(
        SkillExecution execution,
        IReadOnlyList<SkillExecutionStepResult> stepResults)
    {
        return new AssetLatestExecutionSummaryQueryDto(
            ExecutionId: execution.Id,
            SkillName: execution.SkillName,
            TriggerSource: execution.TriggerSource,
            StartedAtUtc: execution.StartedAtUtc,
            CompletedAtUtc: execution.CompletedAtUtc,
            Succeeded: execution.Succeeded,
            Steps: stepResults
                .OrderBy(static step => step.StartedAtUtc)
                .Select(static step => new AssetLatestExecutionStepSummaryQueryDto(
                    StepName: step.StepName,
                    Succeeded: step.Succeeded,
                    ErrorMessage: step.ErrorMessage,
                    StartedAtUtc: step.StartedAtUtc,
                    CompletedAtUtc: step.CompletedAtUtc))
                .ToList());
    }
}

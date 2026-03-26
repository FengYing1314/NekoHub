using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Assets.Queries;
using NekoHub.Application.Assets.Queries.Dtos;
using NekoHub.Application.Common.Models;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreAssetRepository(AssetDbContext dbContext) : IAssetRepository
{
    public async Task AddAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        await dbContext.Assets.AddAsync(asset, cancellationToken);
    }

    public Task<Asset?> GetByIdAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        return dbContext.Assets
            .SingleOrDefaultAsync(x => x.Id == assetId, cancellationToken);
    }

    public async Task<PagedResult<Asset>> GetPagedAsync(GetAssetsPagedQuery query, CancellationToken cancellationToken = default)
    {
        var safePage = query.Page <= 0 ? 1 : query.Page;
        var safePageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var efQuery = dbContext.Assets
            .AsNoTracking()
            .AsQueryable();

        efQuery = ApplyQueryFilter(efQuery, query.Query);
        efQuery = ApplyContentTypeFilter(efQuery, query.ContentType);
        efQuery = ApplyStatusFilter(efQuery, query.Status);
        efQuery = ApplySort(efQuery, query.SortBy, query.SortDirection);

        var total = await efQuery.LongCountAsync(cancellationToken);
        var items = await efQuery
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Asset>(items, safePage, safePageSize, total);
    }

    public async Task<AssetUsageStatsQueryDto> GetUsageStatsAsync(CancellationToken cancellationToken = default)
    {
        var activeAssets = dbContext.Assets
            .AsNoTracking()
            .Where(asset => asset.DeletedAtUtc == null && asset.Status != AssetStatus.Deleted);

        var totalAssets = await activeAssets.LongCountAsync(cancellationToken);
        var totalBytes = await activeAssets.SumAsync(asset => (long?)asset.Size, cancellationToken) ?? 0L;

        var totalDerivatives = await dbContext.AssetDerivatives
            .AsNoTracking()
            .Join(
                activeAssets,
                derivative => derivative.SourceAssetId,
                asset => asset.Id,
                static (derivative, _) => derivative)
            .LongCountAsync(cancellationToken);

        var contentTypeBreakdownRows = await activeAssets
            .GroupBy(asset => asset.ContentType)
            .Select(group => new
            {
                ContentType = group.Key,
                Count = group.LongCount(),
                TotalBytes = group.Sum(asset => asset.Size)
            })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.ContentType)
            .ToListAsync(cancellationToken);

        var contentTypeBreakdown = contentTypeBreakdownRows
            .Select(static item => new AssetContentTypeBreakdownQueryDto(
                item.ContentType,
                item.Count,
                item.TotalBytes))
            .ToList();

        var mostActiveSkillRow = await dbContext.SkillExecutions
            .AsNoTracking()
            .Join(
                activeAssets,
                execution => execution.SourceAssetId,
                asset => asset.Id,
                static (execution, _) => execution)
            .GroupBy(execution => execution.SkillName)
            .Select(group => new
            {
                SkillName = group.Key,
                RunCount = group.LongCount()
            })
            .OrderByDescending(group => group.RunCount)
            .ThenBy(group => group.SkillName)
            .FirstOrDefaultAsync(cancellationToken);

        var mostActiveSkill = mostActiveSkillRow is null
            ? null
            : new AssetSkillUsageSummaryQueryDto(
                mostActiveSkillRow.SkillName,
                mostActiveSkillRow.RunCount);

        return new AssetUsageStatsQueryDto(
            TotalAssets: totalAssets,
            TotalBytes: totalBytes,
            TotalDerivatives: totalDerivatives,
            ContentTypeBreakdown: contentTypeBreakdown,
            MostActiveSkill: mostActiveSkill);
    }

    public Task DeleteAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        dbContext.Assets.Remove(asset);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Asset> ApplyQueryFilter(IQueryable<Asset> query, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var likePattern = $"%{keyword.Trim()}%";
        return query.Where(asset =>
            (asset.OriginalFileName != null && EF.Functions.Like(asset.OriginalFileName, likePattern))
            || (asset.Description != null && EF.Functions.Like(asset.Description, likePattern))
            || (asset.AltText != null && EF.Functions.Like(asset.AltText, likePattern)));
    }

    private static IQueryable<Asset> ApplyContentTypeFilter(IQueryable<Asset> query, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return query;
        }

        var normalized = contentType.Trim();
        if (normalized.EndsWith("/*", StringComparison.Ordinal))
        {
            var prefix = normalized[..^1];
            return query.Where(asset => EF.Functions.Like(asset.ContentType, $"{prefix}%"));
        }

        return query.Where(asset => EF.Functions.Like(asset.ContentType, normalized));
    }

    private static IQueryable<Asset> ApplyStatusFilter(IQueryable<Asset> query, AssetStatus? status)
    {
        if (!status.HasValue)
        {
            return query;
        }

        return query.Where(asset => asset.Status == status.Value);
    }

    private static IQueryable<Asset> ApplySort(
        IQueryable<Asset> query,
        AssetListSortBy sortBy,
        AssetListSortDirection sortDirection)
    {
        return (sortBy, sortDirection) switch
        {
            (AssetListSortBy.Size, AssetListSortDirection.Asc) => query
                .OrderBy(asset => asset.Size)
                .ThenByDescending(asset => asset.CreatedAtUtc)
                .ThenBy(asset => asset.Id),
            (AssetListSortBy.Size, AssetListSortDirection.Desc) => query
                .OrderByDescending(asset => asset.Size)
                .ThenByDescending(asset => asset.CreatedAtUtc)
                .ThenByDescending(asset => asset.Id),
            (AssetListSortBy.CreatedAtUtc, AssetListSortDirection.Asc) => query
                .OrderBy(asset => asset.CreatedAtUtc)
                .ThenBy(asset => asset.Id),
            _ => query
                .OrderByDescending(asset => asset.CreatedAtUtc)
                .ThenByDescending(asset => asset.Id)
        };
    }
}

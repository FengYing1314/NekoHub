using NekoHub.Application.Assets.Dtos;
using NekoHub.Application.Assets.Queries.Dtos;
using NekoHub.Domain.Assets;

namespace NekoHub.Api.Mcp.Tools.Models;

public sealed record McpAssetView(
    Guid Id,
    AssetType Type,
    AssetStatus Status,
    string? OriginalFileName,
    string ContentType,
    string Extension,
    long Size,
    int? Width,
    int? Height,
    string? ChecksumSha256,
    string? PublicUrl,
    string? Description,
    string? AltText,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<McpAssetDerivativeView> Derivatives,
    IReadOnlyList<McpAssetStructuredResultView> StructuredResults,
    McpLatestExecutionSummaryView? LatestExecutionSummary);

public sealed record McpAssetDerivativeView(
    string Kind,
    string ContentType,
    string Extension,
    long Size,
    int? Width,
    int? Height,
    string? PublicUrl,
    DateTimeOffset CreatedAtUtc);

public sealed record McpAssetStructuredResultView(
    string Kind,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc);

public sealed record McpLatestExecutionSummaryView(
    Guid ExecutionId,
    string SkillName,
    string TriggerSource,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    bool Succeeded,
    IReadOnlyList<McpLatestExecutionStepSummaryView> Steps);

public sealed record McpLatestExecutionStepSummaryView(
    string StepName,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public sealed record McpAssetListItemView(
    Guid Id,
    AssetType Type,
    AssetStatus Status,
    string? OriginalFileName,
    string ContentType,
    long Size,
    int? Width,
    int? Height,
    string? PublicUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record McpAssetPageView(
    IReadOnlyList<McpAssetListItemView> Items,
    int Page,
    int PageSize,
    long Total);

public sealed record McpAssetContentUrlView(
    Guid Id,
    string ContentUrl,
    bool PreserveMethod);

public sealed record McpDeleteAssetView(
    Guid Id,
    string Status,
    DateTimeOffset DeletedAtUtc);

public sealed record McpBatchDeleteAssetsView(
    int RequestedCount,
    int DeletedCount,
    IReadOnlyList<Guid> NotFoundIds);

public sealed record McpAssetContentTypeBreakdownView(
    string ContentType,
    long Count,
    long TotalBytes);

public sealed record McpAssetSkillUsageSummaryView(
    string SkillName,
    long RunCount);

public sealed record McpAssetUsageStatsView(
    long TotalAssets,
    long TotalBytes,
    long TotalDerivatives,
    IReadOnlyList<McpAssetContentTypeBreakdownView> ContentTypeBreakdown,
    McpAssetSkillUsageSummaryView? MostActiveSkill);

public static class McpAssetToolModelMapper
{
    public static McpAssetView ToView(AssetDetailsQueryDto dto)
    {
        return new McpAssetView(
            Id: dto.Id,
            Type: dto.Type,
            Status: dto.Status,
            OriginalFileName: dto.OriginalFileName,
            ContentType: dto.ContentType,
            Extension: dto.Extension,
            Size: dto.Size,
            Width: dto.Width,
            Height: dto.Height,
            ChecksumSha256: dto.ChecksumSha256,
            PublicUrl: dto.PublicUrl,
            Description: dto.Description,
            AltText: dto.AltText,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc,
            Derivatives: dto.Derivatives
                .Select(static derivative => new McpAssetDerivativeView(
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
                .Select(static result => new McpAssetStructuredResultView(
                    Kind: result.Kind,
                    PayloadJson: result.PayloadJson,
                    CreatedAtUtc: result.CreatedAtUtc))
                .ToList(),
            LatestExecutionSummary: dto.LatestExecutionSummary is null
                ? null
                : new McpLatestExecutionSummaryView(
                    ExecutionId: dto.LatestExecutionSummary.ExecutionId,
                    SkillName: dto.LatestExecutionSummary.SkillName,
                    TriggerSource: dto.LatestExecutionSummary.TriggerSource,
                    StartedAtUtc: dto.LatestExecutionSummary.StartedAtUtc,
                    CompletedAtUtc: dto.LatestExecutionSummary.CompletedAtUtc,
                    Succeeded: dto.LatestExecutionSummary.Succeeded,
                    Steps: dto.LatestExecutionSummary.Steps
                        .Select(static step => new McpLatestExecutionStepSummaryView(
                            StepName: step.StepName,
                            Succeeded: step.Succeeded,
                            ErrorMessage: step.ErrorMessage,
                            StartedAtUtc: step.StartedAtUtc,
                            CompletedAtUtc: step.CompletedAtUtc))
                        .ToList()));
    }

    public static McpAssetPageView ToView(AssetPagedQueryDto dto)
    {
        return new McpAssetPageView(
            Items: dto.Items
                .Select(static item => new McpAssetListItemView(
                    Id: item.Id,
                    Type: item.Type,
                    Status: item.Status,
                    OriginalFileName: item.OriginalFileName,
                    ContentType: item.ContentType,
                    Size: item.Size,
                    Width: item.Width,
                    Height: item.Height,
                    PublicUrl: item.PublicUrl,
                    CreatedAtUtc: item.CreatedAtUtc,
                    UpdatedAtUtc: item.UpdatedAtUtc))
                .ToList(),
            Page: dto.Page,
            PageSize: dto.PageSize,
            Total: dto.Total);
    }

    public static McpAssetContentUrlView ToView(AssetContentRedirectDto dto)
    {
        return new McpAssetContentUrlView(
            Id: dto.Id,
            ContentUrl: dto.RedirectUrl,
            PreserveMethod: dto.PreserveMethod);
    }

    public static McpDeleteAssetView ToView(DeleteAssetResultDto dto)
    {
        return new McpDeleteAssetView(
            Id: dto.Id,
            Status: dto.Status,
            DeletedAtUtc: dto.DeletedAtUtc);
    }

    public static McpBatchDeleteAssetsView ToView(BatchDeleteAssetsResultDto dto)
    {
        return new McpBatchDeleteAssetsView(
            RequestedCount: dto.RequestedCount,
            DeletedCount: dto.DeletedCount,
            NotFoundIds: dto.NotFoundIds);
    }

    public static McpAssetUsageStatsView ToView(AssetUsageStatsQueryDto dto)
    {
        return new McpAssetUsageStatsView(
            TotalAssets: dto.TotalAssets,
            TotalBytes: dto.TotalBytes,
            TotalDerivatives: dto.TotalDerivatives,
            ContentTypeBreakdown: dto.ContentTypeBreakdown
                .Select(static item => new McpAssetContentTypeBreakdownView(
                    ContentType: item.ContentType,
                    Count: item.Count,
                    TotalBytes: item.TotalBytes))
                .ToList(),
            MostActiveSkill: dto.MostActiveSkill is null
                ? null
                : new McpAssetSkillUsageSummaryView(
                    SkillName: dto.MostActiveSkill.SkillName,
                    RunCount: dto.MostActiveSkill.RunCount));
    }
}

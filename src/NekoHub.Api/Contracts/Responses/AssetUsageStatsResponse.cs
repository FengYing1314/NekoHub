namespace NekoHub.Api.Contracts.Responses;

public sealed record AssetContentTypeBreakdownResponse(
    string ContentType,
    long Count,
    long TotalBytes);

public sealed record AssetSkillUsageSummaryResponse(
    string SkillName,
    long RunCount);

public sealed record AssetUsageStatsResponse(
    long TotalAssets,
    long TotalBytes,
    long TotalDerivatives,
    IReadOnlyList<AssetContentTypeBreakdownResponse> ContentTypeBreakdown,
    AssetSkillUsageSummaryResponse? MostActiveSkill);

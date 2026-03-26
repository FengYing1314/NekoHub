namespace NekoHub.Application.Assets.Queries.Dtos;

public sealed record AssetContentTypeBreakdownQueryDto(
    string ContentType,
    long Count,
    long TotalBytes);

public sealed record AssetSkillUsageSummaryQueryDto(
    string SkillName,
    long RunCount);

public sealed record AssetUsageStatsQueryDto(
    long TotalAssets,
    long TotalBytes,
    long TotalDerivatives,
    IReadOnlyList<AssetContentTypeBreakdownQueryDto> ContentTypeBreakdown,
    AssetSkillUsageSummaryQueryDto? MostActiveSkill);

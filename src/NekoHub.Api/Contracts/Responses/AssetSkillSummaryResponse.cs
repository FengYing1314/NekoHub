namespace NekoHub.Api.Contracts.Responses;

public sealed record AssetSkillSummaryResponse(
    string SkillName,
    string Description,
    IReadOnlyList<string> Steps);

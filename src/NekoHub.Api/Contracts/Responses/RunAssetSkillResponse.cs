namespace NekoHub.Api.Contracts.Responses;

public sealed record RunAssetSkillResponse(
    bool Succeeded,
    string SkillName,
    IReadOnlyList<RunAssetSkillStepResponse> Steps,
    AssetResponse Asset);

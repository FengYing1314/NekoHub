namespace NekoHub.Api.Contracts.Responses;

public sealed record RunAssetSkillStepResponse(
    string Name,
    bool Succeeded,
    string? ErrorMessage);

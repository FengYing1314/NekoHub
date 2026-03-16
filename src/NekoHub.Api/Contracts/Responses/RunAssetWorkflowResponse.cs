namespace NekoHub.Api.Contracts.Responses;

public sealed record RunAssetWorkflowResponse(
    Guid AssetId,
    Guid WorkflowId,
    IReadOnlyList<string> SkillIds);

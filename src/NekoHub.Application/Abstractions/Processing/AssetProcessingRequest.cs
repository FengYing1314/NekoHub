using System.Text.Json.Nodes;

namespace NekoHub.Application.Abstractions.Processing;

public sealed record AssetProcessingRequest(
    AssetCreatedProcessingContext Asset,
    string TriggerSource,
    Guid? WorkflowProfileId = null,
    IReadOnlyList<AssetProcessingSkillRequest>? Skills = null);

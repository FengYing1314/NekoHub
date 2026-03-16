using System.Text.Json.Nodes;

namespace NekoHub.Application.Abstractions.Processing;

public sealed record AssetProcessingSkillRequest(
    string SkillId,
    JsonObject? Parameters = null);

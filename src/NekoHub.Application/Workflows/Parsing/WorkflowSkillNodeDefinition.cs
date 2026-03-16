using System.Text.Json.Nodes;

namespace NekoHub.Application.Workflows.Parsing;

public sealed record WorkflowSkillNodeDefinition(
    string SkillId,
    JsonObject? Parameters = null);

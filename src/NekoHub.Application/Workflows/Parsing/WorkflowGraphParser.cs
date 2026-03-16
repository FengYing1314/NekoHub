using System.Text.Json;
using System.Text.Json.Nodes;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Application.Workflows.Parsing;

public sealed class WorkflowGraphParser : IWorkflowGraphParser
{
    public IReadOnlyList<WorkflowSkillNodeDefinition> ExtractSkills(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
        {
            throw new ValidationException(
                "workflow_profile_graph_json_required",
                "GraphJson is required.");
        }

        JsonNode? graphDocument;
        try
        {
            graphDocument = JsonNode.Parse(graphJson);
        }
        catch (JsonException)
        {
            throw new ValidationException(
                "workflow_profile_graph_json_invalid",
                "GraphJson must be a valid workflow graph JSON object.");
        }

        if (graphDocument is not JsonObject graphObject)
        {
            throw new ValidationException(
                "workflow_profile_graph_json_invalid",
                "GraphJson must be a valid workflow graph JSON object.");
        }

        var nodes = graphObject["nodes"] as JsonArray;
        var skills = new List<WorkflowSkillNodeDefinition>();
        foreach (var node in nodes ?? [])
        {
            var resolvedSkillId = ResolveSkillId(node as JsonObject);
            if (resolvedSkillId is null)
            {
                continue;
            }

            skills.Add(new WorkflowSkillNodeDefinition(
                SkillId: resolvedSkillId,
                Parameters: ResolveParameters(node as JsonObject)));
        }

        return skills;
    }

    private static string? ResolveSkillId(JsonObject? node)
    {
        if (node is null)
        {
            return null;
        }

        // 新图结构优先使用 data.skillId，缺失时再退化到 node.type 兼容旧草稿。
        var dataSkillId = TryGetString(node["data"]?["skillId"]);
        if (!string.IsNullOrWhiteSpace(dataSkillId))
        {
            return dataSkillId.Trim();
        }

        var nodeType = TryGetString(node["type"]);
        return string.IsNullOrWhiteSpace(nodeType) ? null : nodeType;
    }

    private static JsonObject? ResolveParameters(JsonObject? node)
    {
        var data = node?["data"] as JsonObject;
        if (data is null)
        {
            return null;
        }

        if (data["parameters"] is JsonObject explicitParameters)
        {
            return explicitParameters.DeepClone() as JsonObject;
        }

        // 旧图结构可能直接把参数平铺在 data 上，这里去掉 skillId 后整体视为参数对象。
        var parameters = data.DeepClone() as JsonObject;
        parameters?.Remove("skillId");

        return parameters is { Count: > 0 } ? parameters : null;
    }

    private static string? TryGetString(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var stringValue)
            ? stringValue
            : null;
    }
}

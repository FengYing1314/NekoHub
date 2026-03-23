namespace NekoHub.Api.Mcp.Tools.Models;

public static class McpSkillToolSchemas
{
    public static object SkillList { get; } = BuildSkillListSchema();

    public static object RunAssetSkill { get; } = BuildRunAssetSkillSchema();

    private static object BuildSkillListSchema()
    {
        return ObjectSchema(
            new Dictionary<string, object?>
            {
                ["skills"] = ArraySchema(
                    ObjectSchema(
                        new Dictionary<string, object?>
                        {
                            ["skillName"] = StringSchema(),
                            ["description"] = StringSchema(),
                            ["steps"] = ArraySchema(StringSchema())
                        },
                        ["skillName", "description", "steps"]))
            },
            ["skills"]);
    }

    private static object BuildRunAssetSkillSchema()
    {
        return ObjectSchema(
            new Dictionary<string, object?>
            {
                ["succeeded"] = BooleanSchema(),
                ["skillName"] = StringSchema(),
                ["steps"] = ArraySchema(
                    ObjectSchema(
                        new Dictionary<string, object?>
                        {
                            ["name"] = StringSchema(),
                            ["succeeded"] = BooleanSchema(),
                            ["errorMessage"] = NullableStringSchema()
                        },
                        ["name", "succeeded"])),
                ["asset"] = McpAssetToolSchemas.AssetDetail
            },
            ["succeeded", "skillName", "steps", "asset"]);
    }

    private static Dictionary<string, object?> ObjectSchema(
        IReadOnlyDictionary<string, object?> properties,
        IReadOnlyList<string> requiredProperties)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = requiredProperties,
            ["additionalProperties"] = false
        };
    }

    private static Dictionary<string, object?> ArraySchema(object items)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "array",
            ["items"] = items
        };
    }

    private static Dictionary<string, object?> StringSchema()
    {
        return new Dictionary<string, object?> { ["type"] = "string" };
    }

    private static Dictionary<string, object?> BooleanSchema()
    {
        return new Dictionary<string, object?> { ["type"] = "boolean" };
    }

    private static Dictionary<string, object?> NullableStringSchema()
    {
        return new Dictionary<string, object?>
        {
            ["anyOf"] = new object[]
            {
                StringSchema(),
                new Dictionary<string, object?> { ["type"] = "null" }
            }
        };
    }
}

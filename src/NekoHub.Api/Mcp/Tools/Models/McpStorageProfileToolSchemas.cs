namespace NekoHub.Api.Mcp.Tools.Models;

public static class McpStorageProfileToolSchemas
{
    public static object StorageProfile { get; } = BuildStorageProfileSchema();

    public static object StorageProfileList { get; } = BuildStorageProfileListSchema();

    public static object DeleteStorageProfile { get; } = ObjectSchema(
        new Dictionary<string, object?>
        {
            ["id"] = StringSchema("uuid"),
            ["wasDefault"] = BooleanSchema(),
            ["status"] = StringSchema(),
            ["deletedAtUtc"] = StringSchema("date-time")
        },
        ["id", "wasDefault", "status", "deletedAtUtc"]);

    public static object NullableObject => new Dictionary<string, object?>
    {
        ["anyOf"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["additionalProperties"] = true
            },
            new Dictionary<string, object?> { ["type"] = "null" }
        }
    };

    private static object BuildStorageProfileListSchema()
    {
        return ObjectSchema(
            new Dictionary<string, object?>
            {
                ["profiles"] = ArraySchema(StorageProfile)
            },
            ["profiles"]);
    }

    private static object BuildStorageProfileSchema()
    {
        return ObjectSchema(
            new Dictionary<string, object?>
            {
                ["id"] = StringSchema("uuid"),
                ["name"] = StringSchema(),
                ["displayName"] = NullableStringSchema(),
                ["providerType"] = StringSchema(),
                ["isEnabled"] = BooleanSchema(),
                ["isDefault"] = BooleanSchema(),
                ["capabilities"] = ObjectSchema(
                    new Dictionary<string, object?>
                    {
                        ["supportsPublicRead"] = BooleanSchema(),
                        ["supportsPrivateRead"] = BooleanSchema(),
                        ["supportsVisibilityToggle"] = BooleanSchema(),
                        ["supportsDelete"] = BooleanSchema(),
                        ["supportsDirectPublicUrl"] = BooleanSchema(),
                        ["requiresAccessProxy"] = BooleanSchema(),
                        ["recommendedForPrimaryStorage"] = BooleanSchema(),
                        ["isPlatformBacked"] = BooleanSchema(),
                        ["isExperimental"] = BooleanSchema(),
                        ["requiresTokenForPrivateRead"] = BooleanSchema()
                    },
                    [
                        "supportsPublicRead",
                        "supportsPrivateRead",
                        "supportsVisibilityToggle",
                        "supportsDelete",
                        "supportsDirectPublicUrl",
                        "requiresAccessProxy",
                        "recommendedForPrimaryStorage",
                        "isPlatformBacked",
                        "isExperimental",
                        "requiresTokenForPrivateRead"
                    ]),
                ["configurationSummary"] = ObjectSchema(
                    new Dictionary<string, object?>
                    {
                        ["providerName"] = NullableStringSchema(),
                        ["rootPath"] = NullableStringSchema(),
                        ["endpointHost"] = NullableStringSchema(),
                        ["bucketOrContainer"] = NullableStringSchema(),
                        ["region"] = NullableStringSchema(),
                        ["publicBaseUrl"] = NullableStringSchema(),
                        ["forcePathStyle"] = NullableBooleanSchema(),
                        ["owner"] = NullableStringSchema(),
                        ["repository"] = NullableStringSchema(),
                        ["reference"] = NullableStringSchema(),
                        ["releaseTagMode"] = NullableStringSchema(),
                        ["fixedTag"] = NullableStringSchema(),
                        ["pathPrefix"] = NullableStringSchema(),
                        ["visibilityPolicy"] = NullableStringSchema(),
                        ["basePath"] = NullableStringSchema(),
                        ["assetPathPrefix"] = NullableStringSchema(),
                        ["apiBaseUrl"] = NullableStringSchema(),
                        ["rawBaseUrl"] = NullableStringSchema()
                    },
                    []),
                ["createdAtUtc"] = StringSchema("date-time"),
                ["updatedAtUtc"] = StringSchema("date-time")
            },
            ["id", "name", "providerType", "isEnabled", "isDefault", "capabilities", "configurationSummary", "createdAtUtc", "updatedAtUtc"]);
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

    private static Dictionary<string, object?> StringSchema(string? format = null)
    {
        var schema = new Dictionary<string, object?> { ["type"] = "string" };
        if (!string.IsNullOrWhiteSpace(format))
        {
            schema["format"] = format;
        }

        return schema;
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

    private static Dictionary<string, object?> NullableBooleanSchema()
    {
        return new Dictionary<string, object?>
        {
            ["anyOf"] = new object[]
            {
                BooleanSchema(),
                new Dictionary<string, object?> { ["type"] = "null" }
            }
        };
    }
}

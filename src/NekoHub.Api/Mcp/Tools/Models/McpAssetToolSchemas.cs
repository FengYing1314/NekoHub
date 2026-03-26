namespace NekoHub.Api.Mcp.Tools.Models;

public static class McpAssetToolSchemas
{
    public static object NullableString => NullableStringSchema();

    public static object UploadAssetInput { get; } = BuildUploadAssetInputSchema();

    public static object AssetDetail { get; } = BuildAssetDetailSchema();

    public static object AssetPage { get; } = BuildAssetPageSchema();

    public static object AssetContentUrl { get; } = ObjectSchema(
        new Dictionary<string, object?>
        {
            ["id"] = StringSchema("uuid"),
            ["contentUrl"] = StringSchema("uri"),
            ["preserveMethod"] = BooleanSchema()
        },
        ["id", "contentUrl", "preserveMethod"]);

    public static object DeleteAsset { get; } = ObjectSchema(
        new Dictionary<string, object?>
        {
            ["id"] = StringSchema("uuid"),
            ["status"] = StringSchema(),
            ["deletedAtUtc"] = StringSchema("date-time")
        },
        ["id", "status", "deletedAtUtc"]);

    public static object BatchDeleteAssets { get; } = ObjectSchema(
        new Dictionary<string, object?>
        {
            ["requestedCount"] = IntegerSchema(),
            ["deletedCount"] = IntegerSchema(),
            ["notFoundIds"] = ArraySchema(StringSchema("uuid"))
        },
        ["requestedCount", "deletedCount", "notFoundIds"]);

    public static object AssetUsageStats { get; } = ObjectSchema(
        new Dictionary<string, object?>
        {
            ["totalAssets"] = IntegerSchema(),
            ["totalBytes"] = IntegerSchema(),
            ["totalDerivatives"] = IntegerSchema(),
            ["contentTypeBreakdown"] = ArraySchema(
                ObjectSchema(
                    new Dictionary<string, object?>
                    {
                        ["contentType"] = StringSchema(),
                        ["count"] = IntegerSchema(),
                        ["totalBytes"] = IntegerSchema()
                    },
                    ["contentType", "count", "totalBytes"])),
            ["mostActiveSkill"] = NullableObjectSchema(
                new Dictionary<string, object?>
                {
                    ["skillName"] = StringSchema(),
                    ["runCount"] = IntegerSchema()
                },
                ["skillName", "runCount"])
        },
        ["totalAssets", "totalBytes", "totalDerivatives", "contentTypeBreakdown"]);

    private static object BuildUploadAssetInputSchema()
    {
        return ObjectSchema(
            new Dictionary<string, object?>
            {
                ["fileName"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["minLength"] = 1
                },
                ["contentType"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["minLength"] = 1
                },
                ["contentBase64"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["contentEncoding"] = "base64"
                },
                ["description"] = NullableStringSchema(),
                ["altText"] = NullableStringSchema()
            },
            ["fileName", "contentType", "contentBase64"]);
    }

    private static object BuildAssetDetailSchema()
    {
        return ObjectSchema(
            new Dictionary<string, object?>
            {
                ["id"] = StringSchema("uuid"),
                ["type"] = StringSchema(),
                ["status"] = StringSchema(),
                ["originalFileName"] = NullableStringSchema(),
                ["contentType"] = StringSchema(),
                ["extension"] = StringSchema(),
                ["size"] = IntegerSchema(),
                ["width"] = NullableIntegerSchema(),
                ["height"] = NullableIntegerSchema(),
                ["checksumSha256"] = NullableStringSchema(),
                ["publicUrl"] = NullableStringSchema("uri"),
                ["description"] = NullableStringSchema(),
                ["altText"] = NullableStringSchema(),
                ["createdAtUtc"] = StringSchema("date-time"),
                ["updatedAtUtc"] = StringSchema("date-time"),
                ["derivatives"] = ArraySchema(
                    ObjectSchema(
                        new Dictionary<string, object?>
                        {
                            ["kind"] = StringSchema(),
                            ["contentType"] = StringSchema(),
                            ["extension"] = StringSchema(),
                            ["size"] = IntegerSchema(),
                            ["width"] = NullableIntegerSchema(),
                            ["height"] = NullableIntegerSchema(),
                            ["publicUrl"] = NullableStringSchema("uri"),
                            ["createdAtUtc"] = StringSchema("date-time")
                        },
                        ["kind", "contentType", "extension", "size", "createdAtUtc"])),
                ["structuredResults"] = ArraySchema(
                    ObjectSchema(
                        new Dictionary<string, object?>
                        {
                            ["kind"] = StringSchema(),
                            ["payloadJson"] = StringSchema(),
                            ["createdAtUtc"] = StringSchema("date-time")
                        },
                        ["kind", "payloadJson", "createdAtUtc"])),
                ["latestExecutionSummary"] = NullableObjectSchema(
                    new Dictionary<string, object?>
                    {
                        ["executionId"] = StringSchema("uuid"),
                        ["skillName"] = StringSchema(),
                        ["triggerSource"] = StringSchema(),
                        ["startedAtUtc"] = StringSchema("date-time"),
                        ["completedAtUtc"] = StringSchema("date-time"),
                        ["succeeded"] = BooleanSchema(),
                        ["steps"] = ArraySchema(
                            ObjectSchema(
                                new Dictionary<string, object?>
                                {
                                    ["stepName"] = StringSchema(),
                                    ["succeeded"] = BooleanSchema(),
                                    ["errorMessage"] = NullableStringSchema(),
                                    ["startedAtUtc"] = StringSchema("date-time"),
                                    ["completedAtUtc"] = StringSchema("date-time")
                                },
                                ["stepName", "succeeded", "startedAtUtc", "completedAtUtc"]))
                    },
                    ["executionId", "skillName", "triggerSource", "startedAtUtc", "completedAtUtc", "succeeded", "steps"])
            },
            [
                "id",
                "type",
                "status",
                "originalFileName",
                "contentType",
                "extension",
                "size",
                "createdAtUtc",
                "updatedAtUtc",
                "derivatives",
                "structuredResults"
            ]);
    }

    private static object BuildAssetPageSchema()
    {
        return ObjectSchema(
            new Dictionary<string, object?>
            {
                ["items"] = ArraySchema(
                    ObjectSchema(
                        new Dictionary<string, object?>
                        {
                            ["id"] = StringSchema("uuid"),
                            ["type"] = StringSchema(),
                            ["status"] = StringSchema(),
                ["originalFileName"] = NullableStringSchema(),
                            ["contentType"] = StringSchema(),
                            ["size"] = IntegerSchema(),
                            ["width"] = NullableIntegerSchema(),
                            ["height"] = NullableIntegerSchema(),
                            ["publicUrl"] = NullableStringSchema("uri"),
                            ["createdAtUtc"] = StringSchema("date-time"),
                            ["updatedAtUtc"] = StringSchema("date-time")
                        },
                        [
                            "id",
                            "type",
                            "status",
                            "originalFileName",
                            "contentType",
                            "size",
                            "createdAtUtc",
                            "updatedAtUtc"
                        ])),
                ["page"] = IntegerSchema(),
                ["pageSize"] = IntegerSchema(),
                ["total"] = IntegerSchema()
            },
            ["items", "page", "pageSize", "total"]);
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

    private static Dictionary<string, object?> IntegerSchema()
    {
        return new Dictionary<string, object?> { ["type"] = "integer" };
    }

    private static Dictionary<string, object?> BooleanSchema()
    {
        return new Dictionary<string, object?> { ["type"] = "boolean" };
    }

    private static Dictionary<string, object?> NullableStringSchema(string? format = null)
    {
        return new Dictionary<string, object?>
        {
            ["anyOf"] = new object[]
            {
                StringSchema(format),
                new Dictionary<string, object?> { ["type"] = "null" }
            }
        };
    }

    private static Dictionary<string, object?> NullableIntegerSchema()
    {
        return new Dictionary<string, object?>
        {
            ["anyOf"] = new object[]
            {
                IntegerSchema(),
                new Dictionary<string, object?> { ["type"] = "null" }
            }
        };
    }

    private static Dictionary<string, object?> NullableObjectSchema(
        IReadOnlyDictionary<string, object?> properties,
        IReadOnlyList<string> requiredProperties)
    {
        return new Dictionary<string, object?>
        {
            ["anyOf"] = new object[]
            {
                ObjectSchema(properties, requiredProperties),
                new Dictionary<string, object?> { ["type"] = "null" }
            }
        };
    }
}

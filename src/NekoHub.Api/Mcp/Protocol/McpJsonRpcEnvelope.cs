using System.Text.Json;

namespace NekoHub.Api.Mcp.Protocol;

internal sealed record McpJsonRpcEnvelope(
    string? JsonRpc,
    string? Method,
    JsonElement? Id,
    JsonElement? Params,
    JsonElement? Result,
    JsonElement? Error)
{
    public bool IsRequest => Id.HasValue && !string.IsNullOrWhiteSpace(Method);

    public bool IsNotification => !Id.HasValue && !string.IsNullOrWhiteSpace(Method);

    public bool IsResponse => Id.HasValue && string.IsNullOrWhiteSpace(Method) && (Result.HasValue || Error.HasValue);

    public static bool TryParse(JsonElement payload, out McpJsonRpcEnvelope? envelope, out string errorMessage)
    {
        if (payload.ValueKind is not JsonValueKind.Object)
        {
            envelope = null;
            errorMessage = "JSON-RPC payload must be an object.";
            return false;
        }

        string? jsonRpc = null;
        if (payload.TryGetProperty("jsonrpc", out var jsonRpcElement))
        {
            if (jsonRpcElement.ValueKind is not JsonValueKind.String)
            {
                envelope = null;
                errorMessage = "The 'jsonrpc' field must be a string.";
                return false;
            }

            jsonRpc = jsonRpcElement.GetString();
        }

        string? method = null;
        if (payload.TryGetProperty("method", out var methodElement))
        {
            if (methodElement.ValueKind is not JsonValueKind.String)
            {
                envelope = null;
                errorMessage = "The 'method' field must be a string.";
                return false;
            }

            method = methodElement.GetString();
        }

        JsonElement? id = null;
        if (payload.TryGetProperty("id", out var idElement))
        {
            if (idElement.ValueKind is not JsonValueKind.String and not JsonValueKind.Number and not JsonValueKind.Null)
            {
                envelope = null;
                errorMessage = "The 'id' field must be a string, number, or null.";
                return false;
            }

            if (idElement.ValueKind is not JsonValueKind.Null)
            {
                id = idElement.Clone();
            }
        }

        var parameters = payload.TryGetProperty("params", out var paramsElement)
            ? paramsElement.Clone()
            : (JsonElement?)null;
        var result = payload.TryGetProperty("result", out var resultElement)
            ? resultElement.Clone()
            : (JsonElement?)null;
        var error = payload.TryGetProperty("error", out var errorElement)
            ? errorElement.Clone()
            : (JsonElement?)null;

        envelope = new McpJsonRpcEnvelope(jsonRpc, method, id, parameters, result, error);
        errorMessage = string.Empty;
        return true;
    }
}

using System.Reflection;
using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Prompts;
using NekoHub.Api.Mcp.Resources;
using NekoHub.Api.Mcp.Tools;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp;

public sealed class McpServer(
    McpPromptRegistry promptRegistry,
    McpResourceRegistry resourceRegistry,
    McpToolRegistry toolRegistry,
    ILogger<McpServer> logger) : IMcpServer
{
    private const int ParseErrorCode = -32700;
    private const int InvalidRequestCode = -32600;
    private const int MethodNotFoundCode = -32601;
    private const int InvalidParamsCode = -32602;
    private const int InternalErrorCode = -32603;

    public McpHttpResponse HandleGet()
    {
        return new McpHttpResponse(
            StatusCode: StatusCodes.Status405MethodNotAllowed,
            Headers: new Dictionary<string, string> { ["Allow"] = "POST" });
    }

    public async Task<McpHttpResponse> HandlePostAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!IsOriginAllowed(request))
        {
            return BuildErrorResponse(
                StatusCodes.Status403Forbidden,
                null,
                InvalidRequestCode,
                "Origin header is not allowed.");
        }

        if (!TryResolveRequestProtocolVersion(request, out var requestProtocolVersion, out var protocolVersionError))
        {
            return BuildErrorResponse(
                StatusCodes.Status400BadRequest,
                null,
                InvalidRequestCode,
                protocolVersionError!);
        }

        JsonDocument payloadDocument;
        try
        {
            payloadDocument = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return BuildErrorResponse(
                StatusCodes.Status400BadRequest,
                null,
                ParseErrorCode,
                "Failed to parse JSON-RPC payload.",
                requestProtocolVersion);
        }

        using (payloadDocument)
        {
            if (payloadDocument.RootElement.ValueKind is JsonValueKind.Array)
            {
                return BuildErrorResponse(
                    StatusCodes.Status400BadRequest,
                    null,
                    InvalidRequestCode,
                    "Batch JSON-RPC payloads are not supported.",
                    requestProtocolVersion);
            }

            if (!McpJsonRpcEnvelope.TryParse(payloadDocument.RootElement, out var envelope, out var parseError))
            {
                return BuildErrorResponse(
                    StatusCodes.Status400BadRequest,
                    null,
                    InvalidRequestCode,
                    parseError,
                    requestProtocolVersion);
            }

            return await DispatchAsync(
                envelope!,
                requestProtocolVersion ?? McpProtocolConstants.LatestProtocolVersion,
                cancellationToken);
        }
    }

    private async Task<McpHttpResponse> DispatchAsync(
        McpJsonRpcEnvelope envelope,
        string requestProtocolVersion,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(envelope.JsonRpc, McpProtocolConstants.JsonRpcVersion, StringComparison.Ordinal))
        {
            return BuildErrorResponse(
                StatusCodes.Status400BadRequest,
                envelope.Id,
                InvalidRequestCode,
                "Unsupported JSON-RPC version.",
                requestProtocolVersion);
        }

        if (envelope.IsResponse)
        {
            return new McpHttpResponse(StatusCodes.Status202Accepted);
        }

        if (envelope.IsNotification)
        {
            return new McpHttpResponse(StatusCodes.Status202Accepted);
        }

        if (!envelope.IsRequest || !envelope.Id.HasValue || string.IsNullOrWhiteSpace(envelope.Method))
        {
            return BuildErrorResponse(
                StatusCodes.Status400BadRequest,
                envelope.Id,
                InvalidRequestCode,
                "JSON-RPC request envelope is invalid.",
                requestProtocolVersion);
        }

        return envelope.Method switch
        {
            "initialize" => HandleInitialize(envelope.Id.Value, envelope.Params),
            "ping" => BuildSuccessResponse(envelope.Id.Value, new { }, requestProtocolVersion),
            "tools/list" => BuildSuccessResponse(
                envelope.Id.Value,
                new McpListToolsResult(toolRegistry.GetDefinitions()),
                requestProtocolVersion),
            "tools/call" => await HandleToolCallAsync(
                envelope.Id.Value,
                envelope.Params,
                requestProtocolVersion,
                cancellationToken),
            "resources/list" => await HandleResourcesListAsync(
                envelope.Id.Value,
                requestProtocolVersion,
                cancellationToken),
            "resources/read" => await HandleResourceReadAsync(
                envelope.Id.Value,
                envelope.Params,
                requestProtocolVersion,
                cancellationToken),
            "prompts/list" => await HandlePromptsListAsync(
                envelope.Id.Value,
                requestProtocolVersion),
            "prompts/get" => await HandlePromptGetAsync(
                envelope.Id.Value,
                envelope.Params,
                requestProtocolVersion,
                cancellationToken),
            _ => BuildErrorResponse(
                StatusCodes.Status200OK,
                envelope.Id,
                MethodNotFoundCode,
                $"Method '{envelope.Method}' was not found.",
                requestProtocolVersion)
        };
    }

    private McpHttpResponse HandleInitialize(JsonElement requestId, JsonElement? parameters)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } parameterObject
            || !parameterObject.TryGetProperty("protocolVersion", out var protocolVersionElement)
            || protocolVersionElement.ValueKind is not JsonValueKind.String)
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                "initialize params.protocolVersion is required.");
        }

        var requestedProtocolVersion = protocolVersionElement.GetString();
        var negotiatedProtocolVersion = McpProtocolConstants.IsSupportedProtocolVersion(requestedProtocolVersion)
            ? requestedProtocolVersion!
            : McpProtocolConstants.LatestProtocolVersion;

        var result = new McpInitializeResult(
            ProtocolVersion: negotiatedProtocolVersion,
            Capabilities: new McpServerCapabilities
            {
                Tools = new McpToolsCapability
                {
                    ListChanged = false
                },
                Resources = new McpResourcesCapability
                {
                    Subscribe = false,
                    ListChanged = false
                },
                Prompts = new McpPromptsCapability
                {
                    ListChanged = false
                }
            },
            ServerInfo: new McpImplementation(
                Name: "NekoHub MCP",
                Version: ResolveServerVersion())
            {
                Title = "NekoHub Asset, Skill, Resource, and Prompt Surface"
            })
        {
            Instructions =
                "Use tools, resources, and prompts to inspect assets, run skill pipelines, and manage content. Responses reuse mature read models and intentionally omit storage/provider internals."
        };

        return BuildSuccessResponse(requestId, result, negotiatedProtocolVersion);
    }

    private async Task<McpHttpResponse> HandleToolCallAsync(
        JsonElement requestId,
        JsonElement? parameters,
        string requestProtocolVersion,
        CancellationToken cancellationToken)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } parameterObject)
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                "tools/call params must be an object.",
                requestProtocolVersion);
        }

        if (!parameterObject.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(nameElement.GetString()))
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                "tools/call params.name is required.",
                requestProtocolVersion);
        }

        var toolName = nameElement.GetString()!;
        if (!toolRegistry.TryGet(toolName, out var tool) || tool is null)
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                MethodNotFoundCode,
                $"Tool '{toolName}' was not found.",
                requestProtocolVersion);
        }

        var arguments = parameterObject.TryGetProperty("arguments", out var argumentElement)
            ? argumentElement.Clone()
            : (JsonElement?)null;

        try
        {
            var toolResult = await tool.InvokeAsync(arguments, cancellationToken);
            return BuildSuccessResponse(
                requestId,
                McpToolResponseFactory.Success(toolResult.StructuredContent),
                requestProtocolVersion);
        }
        catch (AppException exception)
        {
            return BuildSuccessResponse(
                requestId,
                McpToolResponseFactory.Error(exception.Code, exception.Message),
                requestProtocolVersion);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while executing MCP tool {ToolName}", toolName);
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InternalErrorCode,
                $"Tool '{toolName}' failed unexpectedly.",
                requestProtocolVersion);
        }
    }

    private async Task<McpHttpResponse> HandleResourcesListAsync(
        JsonElement requestId,
        string requestProtocolVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var resources = await resourceRegistry.GetDefinitionsAsync(cancellationToken);
            return BuildSuccessResponse(
                requestId,
                new McpListResourcesResult(resources),
                requestProtocolVersion);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while listing MCP resources.");
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InternalErrorCode,
                "Failed to list MCP resources.",
                requestProtocolVersion);
        }
    }

    private async Task<McpHttpResponse> HandleResourceReadAsync(
        JsonElement requestId,
        JsonElement? parameters,
        string requestProtocolVersion,
        CancellationToken cancellationToken)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } parameterObject)
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                "resources/read params must be an object.",
                requestProtocolVersion);
        }

        if (!parameterObject.TryGetProperty("uri", out var uriElement)
            || uriElement.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(uriElement.GetString()))
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                "resources/read params.uri is required.",
                requestProtocolVersion);
        }

        var uriText = uriElement.GetString()!;
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var resourceUri))
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                "resources/read params.uri must be a valid absolute URI.",
                requestProtocolVersion);
        }

        if (!resourceRegistry.TryResolve(resourceUri, out var resource) || resource is null)
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                $"Resource '{uriText}' was not found.",
                requestProtocolVersion);
        }

        try
        {
            var readResult = await resource.ReadAsync(resourceUri, cancellationToken);
            return BuildSuccessResponse(
                requestId,
                McpResourceResponseFactory.Success(readResult),
                requestProtocolVersion);
        }
        catch (AppException exception)
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                exception.Message,
                requestProtocolVersion,
                new
                {
                    code = exception.Code
                });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while reading MCP resource {ResourceUri}", uriText);
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InternalErrorCode,
                $"Resource '{uriText}' failed unexpectedly.",
                requestProtocolVersion);
        }
    }

    private Task<McpHttpResponse> HandlePromptsListAsync(
        JsonElement requestId,
        string requestProtocolVersion)
    {
        try
        {
            var prompts = promptRegistry.GetDefinitions();
            return Task.FromResult(BuildSuccessResponse(
                requestId,
                new McpListPromptsResult(prompts),
                requestProtocolVersion));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while listing MCP prompts.");
            return Task.FromResult(BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InternalErrorCode,
                "Failed to list MCP prompts.",
                requestProtocolVersion));
        }
    }

    private async Task<McpHttpResponse> HandlePromptGetAsync(
        JsonElement requestId,
        JsonElement? parameters,
        string requestProtocolVersion,
        CancellationToken cancellationToken)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } parameterObject)
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                "prompts/get params must be an object.",
                requestProtocolVersion);
        }

        if (!parameterObject.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(nameElement.GetString()))
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                "prompts/get params.name is required.",
                requestProtocolVersion);
        }

        var promptName = nameElement.GetString()!;
        if (!promptRegistry.TryGet(promptName, out var prompt) || prompt is null)
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                MethodNotFoundCode,
                $"Prompt '{promptName}' was not found.",
                requestProtocolVersion,
                new
                {
                    code = "prompt_not_found"
                });
        }

        var arguments = parameterObject.TryGetProperty("arguments", out var argumentElement)
            ? argumentElement.Clone()
            : (JsonElement?)null;

        try
        {
            var promptResult = await prompt.InvokeAsync(arguments, cancellationToken);
            return BuildSuccessResponse(
                requestId,
                new McpGetPromptResult(promptResult.Name, promptResult.Messages)
                {
                    Description = promptResult.Description
                },
                requestProtocolVersion);
        }
        catch (AppException exception)
        {
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InvalidParamsCode,
                exception.Message,
                requestProtocolVersion,
                new
                {
                    code = exception.Code
                });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while getting MCP prompt {PromptName}", promptName);
            return BuildErrorResponse(
                StatusCodes.Status200OK,
                requestId,
                InternalErrorCode,
                $"Prompt '{promptName}' failed unexpectedly.",
                requestProtocolVersion);
        }
    }

    private static bool TryResolveRequestProtocolVersion(
        HttpRequest request,
        out string? requestProtocolVersion,
        out string? errorMessage)
    {
        requestProtocolVersion = null;
        errorMessage = null;

        if (!request.Headers.TryGetValue(McpProtocolConstants.ProtocolVersionHeader, out var protocolHeaderValues))
        {
            return true;
        }

        var headerValue = protocolHeaderValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return true;
        }

        if (!McpProtocolConstants.IsSupportedProtocolVersion(headerValue))
        {
            errorMessage = $"Unsupported MCP protocol version '{headerValue}'.";
            return false;
        }

        requestProtocolVersion = headerValue;
        return true;
    }

    private static bool IsOriginAllowed(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Origin", out var originValues))
        {
            return true;
        }

        var origin = originValues.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            return false;
        }

        var scheme = GetForwardedHeaderValue(request, "X-Forwarded-Proto") ?? request.Scheme;
        var authority = GetForwardedHeaderValue(request, "X-Forwarded-Host") ?? request.Host.Value;

        return string.Equals(originUri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(originUri.Authority, authority, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetForwardedHeaderValue(HttpRequest request, string headerName)
    {
        if (!request.Headers.TryGetValue(headerName, out var values))
        {
            return null;
        }

        return values.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private static string ResolveServerVersion()
    {
        return typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? typeof(Program).Assembly.GetName().Version?.ToString(3)
               ?? "1.0.0";
    }

    private static McpHttpResponse BuildSuccessResponse(JsonElement requestId, object result, string protocolVersion)
    {
        return new McpHttpResponse(
            StatusCode: StatusCodes.Status200OK,
            Body: new McpJsonRpcSuccessResponse(requestId, result),
            Headers: CreateJsonHeaders(protocolVersion));
    }

    private static McpHttpResponse BuildErrorResponse(
        int statusCode,
        JsonElement? requestId,
        int errorCode,
        string message,
        string? protocolVersion = null,
        object? data = null)
    {
        return new McpHttpResponse(
            StatusCode: statusCode,
            Body: new McpJsonRpcErrorResponse(requestId, new McpJsonRpcError(errorCode, message, data)),
            Headers: CreateJsonHeaders(protocolVersion));
    }

    private static IReadOnlyDictionary<string, string> CreateJsonHeaders(string? protocolVersion)
    {
        var headers = new Dictionary<string, string>
        {
            ["Cache-Control"] = "no-store"
        };

        if (!string.IsNullOrWhiteSpace(protocolVersion))
        {
            headers[McpProtocolConstants.ProtocolVersionHeader] = protocolVersion;
        }

        return headers;
    }
}

using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using NekoHub.Api.Auth;
using NekoHub.Api.Mcp;
using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("mcp")]
[Authorize(Policy = ApiKeyAuthorization.PolicyName)]
public sealed class McpController(
    IMcpServer mcpServer,
    IMcpSessionManager mcpSessionManager) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return ToActionResult(mcpServer.HandleGet());
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> PostAsync(CancellationToken cancellationToken)
    {
        var response = await mcpServer.HandlePostAsync(Request, cancellationToken);
        return ToActionResult(response);
    }

    [HttpGet("sse")]
    public async Task GetSseAsync(CancellationToken cancellationToken)
    {
        var session = mcpSessionManager.CreateSession();
        var endpointUri = BuildMessageEndpoint(session.SessionId);

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await WriteSseEventAsync("endpoint", endpointUri, cancellationToken);

        try
        {
            await foreach (var message in session.Reader.ReadAllAsync(HttpContext.RequestAborted))
            {
                await WriteSseEventAsync("message", message, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
        }
        finally
        {
            mcpSessionManager.RemoveSession(session.SessionId);
        }
    }

    [HttpPost("message")]
    [Consumes("application/json")]
    public async Task<IActionResult> PostMessageAsync([FromQuery] string? sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "Query parameter 'sessionId' is required." });
        }

        if (!mcpSessionManager.TryGetWriter(sessionId, out var writer) || writer is null)
        {
            return NotFound(new { error = $"MCP session '{sessionId}' was not found." });
        }

        var response = await mcpServer.HandlePostAsync(Request, cancellationToken);
        if (response.Body is not null)
        {
            var payload = JsonSerializer.Serialize(response.Body, McpJsonOptions.Default);

            try
            {
                await writer.WriteAsync(payload, cancellationToken);
            }
            catch (ChannelClosedException)
            {
                mcpSessionManager.RemoveSession(sessionId);
                return NotFound(new { error = $"MCP session '{sessionId}' was closed." });
            }
        }

        return Accepted();
    }

    private IActionResult ToActionResult(McpHttpResponse response)
    {
        if (response.Headers is not null)
        {
            foreach (var header in response.Headers)
            {
                Response.Headers[header.Key] = header.Value;
            }
        }

        if (response.Body is null)
        {
            return StatusCode(response.StatusCode);
        }

        return new ContentResult
        {
            StatusCode = response.StatusCode,
            ContentType = "application/json",
            Content = JsonSerializer.Serialize(response.Body, McpJsonOptions.Default)
        };
    }

    private string BuildMessageEndpoint(string sessionId)
    {
        return UriHelper.BuildAbsolute(
            Request.Scheme,
            Request.Host,
            Request.PathBase,
            "/mcp/message",
            QueryString.Create("sessionId", sessionId));
    }

    private async Task WriteSseEventAsync(string eventName, string data, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}

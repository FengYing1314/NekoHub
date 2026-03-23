using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NekoHub.Api.Auth;
using NekoHub.Api.Mcp;
using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("mcp")]
[Authorize(Policy = ApiKeyAuthorization.PolicyName)]
public sealed class McpController(McpServer mcpServer) : ControllerBase
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
}

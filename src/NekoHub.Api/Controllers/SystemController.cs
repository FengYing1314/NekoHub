using Microsoft.AspNetCore.Mvc;
using NekoHub.Api.Contracts.Responses;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("ping")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        var payload = new
        {
            status = "ok",
            timestampUtc = DateTimeOffset.UtcNow
        };

        return Ok(ApiResponseFactory.Success<object>(payload));
    }
}

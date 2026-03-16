using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NekoHub.Api.Configuration;
using NekoHub.Api.Contracts.Responses;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(
    IOptions<ApiKeyAuthOptions> apiKeyAuthOptions,
    IOptions<AssetApiOptions> assetApiOptions) : ControllerBase
{
    private readonly ApiKeyAuthOptions _apiKeyAuthOptions = apiKeyAuthOptions.Value;
    private readonly AssetApiOptions _assetApiOptions = assetApiOptions.Value;

    [HttpGet("ping")]
    [AllowAnonymous]
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

    [HttpGet("bootstrap")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SystemBootstrapResponse>), StatusCodes.Status200OK)]
    public IActionResult Bootstrap()
    {
        var response = new SystemBootstrapResponse(
            ApiKeyRequired: _apiKeyAuthOptions.Enabled,
            MaxUploadSizeBytes: _assetApiOptions.MaxUploadSizeBytes,
            AllowedContentTypes: _assetApiOptions.AllowedContentTypes);

        return Ok(ApiResponseFactory.Success(response));
    }
}

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NekoHub.Api.Configuration;
using NekoHub.Api.Contracts.Responses;

namespace NekoHub.Api.Auth;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<ApiKeyAuthOptions> apiKeyAuthOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder)
{
    private const string FailureCodeContextKey = "__api_key_failure_code";
    private static readonly ClaimsPrincipal DisabledAuthPrincipal = BuildDisabledAuthPrincipal();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authOptions = apiKeyAuthOptions.Value;
        if (!authOptions.Enabled)
        {
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(DisabledAuthPrincipal, Scheme.Name)));
        }

        if (!Request.Headers.TryGetValue("Authorization", out var rawAuthorizationHeader)
            || string.IsNullOrWhiteSpace(rawAuthorizationHeader))
        {
            return Task.FromResult(Fail("api_key_missing", "API key is required."));
        }

        var authorizationHeader = rawAuthorizationHeader.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue)
            || !string.Equals(headerValue.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(headerValue.Parameter))
        {
            return Task.FromResult(Fail("api_key_invalid", "Authorization header must use Bearer scheme."));
        }

        var providedApiKey = headerValue.Parameter.Trim();
        if (!IsAllowedApiKey(providedApiKey, authOptions.Keys))
        {
            return Task.FromResult(Fail("api_key_invalid", "Provided API key is invalid."));
        }

        Context.Items.Remove(FailureCodeContextKey);
        var principal = BuildPrincipal(providedApiKey);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Response.HasStarted)
        {
            return;
        }

        var failureCode = ResolveFailureCode();
        var message = failureCode == "api_key_missing"
            ? "API key is required. Use Authorization: Bearer <API_KEY>."
            : "API key is invalid.";

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        Response.Headers["WWW-Authenticate"] = "Bearer realm=\"NekoHub\"";

        var body = new ApiErrorResponse(
            new ApiError(
                Code: failureCode,
                Message: message,
                TraceId: Context.TraceIdentifier,
                Status: StatusCodes.Status401Unauthorized));

        await Response.WriteAsJsonAsync(body);
    }

    private AuthenticateResult Fail(string code, string message)
    {
        Context.Items[FailureCodeContextKey] = code;
        return AuthenticateResult.Fail(message);
    }

    private string ResolveFailureCode()
    {
        return Context.Items.TryGetValue(FailureCodeContextKey, out var code)
               && code is string value
               && !string.IsNullOrWhiteSpace(value)
            ? value
            : "api_key_missing";
    }

    private static ClaimsPrincipal BuildPrincipal(string providedApiKey)
    {
        var keyFingerprint = ComputeFingerprint(providedApiKey);
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, keyFingerprint),
                new Claim(ClaimTypes.Name, $"api-key:{keyFingerprint}")
            ],
            ApiKeyAuthenticationDefaults.SchemeName);

        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal BuildDisabledAuthPrincipal()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "auth-disabled"),
                new Claim(ClaimTypes.Name, "auth-disabled")
            ],
            ApiKeyAuthenticationDefaults.SchemeName);

        return new ClaimsPrincipal(identity);
    }

    private static bool IsAllowedApiKey(string providedApiKey, IReadOnlyList<string> configuredApiKeys)
    {
        foreach (var configuredApiKey in configuredApiKeys)
        {
            if (string.IsNullOrWhiteSpace(configuredApiKey))
            {
                continue;
            }

            if (FixedTimeEquals(providedApiKey, configuredApiKey.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length
               && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string ComputeFingerprint(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}

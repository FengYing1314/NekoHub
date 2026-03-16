namespace NekoHub.Api.Contracts.Requests;

public sealed record LoginRequest(
    string Username,
    string Password);

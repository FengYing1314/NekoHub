namespace NekoHub.Api.Contracts.Responses;

public sealed record AiProviderProfileTestResponse(
    bool Succeeded,
    string? Caption,
    string ResolvedModelName,
    string ResolvedApiBaseUrl,
    string? ErrorMessage);

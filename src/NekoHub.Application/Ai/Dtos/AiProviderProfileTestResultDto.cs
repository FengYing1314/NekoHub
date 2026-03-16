namespace NekoHub.Application.Ai.Dtos;

public sealed record AiProviderProfileTestResultDto(
    bool Succeeded,
    string? Caption,
    string ResolvedModelName,
    string ResolvedApiBaseUrl,
    string? ErrorMessage);

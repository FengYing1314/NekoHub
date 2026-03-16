namespace NekoHub.Api.Contracts.Responses;

public sealed record SystemBootstrapResponse(
    bool ApiKeyRequired,
    long MaxUploadSizeBytes,
    IReadOnlyList<string> AllowedContentTypes);

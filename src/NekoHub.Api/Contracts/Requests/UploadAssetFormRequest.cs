using Microsoft.AspNetCore.Http;

namespace NekoHub.Api.Contracts.Requests;

public sealed class UploadAssetFormRequest
{
    public IFormFile? File { get; init; }

    public string? Description { get; init; }

    public string? AltText { get; init; }

    public bool? IsPublic { get; init; }

    public Guid? StorageProviderProfileId { get; init; }
}

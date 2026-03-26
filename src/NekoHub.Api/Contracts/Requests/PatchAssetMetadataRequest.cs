using NekoHub.Application.Common.Models;

namespace NekoHub.Api.Contracts.Requests;

public sealed class PatchAssetMetadataRequest
{
    public OptionalValue<string?> Description { get; init; }

    public OptionalValue<string?> AltText { get; init; }

    public OptionalValue<string?> OriginalFileName { get; init; }
}

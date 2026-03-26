using NekoHub.Application.Common.Models;

namespace NekoHub.Application.Assets.Commands;

public sealed record PatchAssetMetadataCommand(
    Guid AssetId,
    OptionalValue<string?> Description,
    OptionalValue<string?> AltText,
    OptionalValue<string?> OriginalFileName);

namespace NekoHub.Application.Assets.Commands;

public sealed record DeleteAssetCommand(
    Guid AssetId,
    string? CommitMessage = null);

namespace NekoHub.Application.Assets.Commands;

public sealed record BatchDeleteAssetsCommand(IReadOnlyList<Guid> AssetIds);

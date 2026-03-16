namespace NekoHub.Application.Abstractions.Storage;

public sealed record StoredAssetObject(
    string Provider,
    string StorageKey,
    string? PublicUrl,
    string? StoredFileName);

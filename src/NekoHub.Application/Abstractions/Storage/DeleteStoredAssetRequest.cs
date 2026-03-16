namespace NekoHub.Application.Abstractions.Storage;

public sealed record DeleteStoredAssetRequest(
    string StorageKey,
    string? CommitMessage = null);

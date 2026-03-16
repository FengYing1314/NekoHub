namespace NekoHub.Application.Abstractions.Storage;

public sealed record StoreAssetRequest(
    string FileName,
    string ContentType,
    string Extension,
    long FileSize);

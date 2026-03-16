namespace NekoHub.Application.Abstractions.Processing;

public sealed record AssetCreatedProcessingContext(
    Guid AssetId,
    string StorageProvider,
    string StorageKey,
    string ContentType,
    string Extension,
    long Size,
    int? Width,
    int? Height,
    string? ChecksumSha256,
    string? PublicUrl,
    DateTimeOffset CreatedAtUtc);

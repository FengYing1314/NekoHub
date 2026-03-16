namespace NekoHub.Application.Abstractions.Metadata;

public sealed record ExtractedAssetMetadata(
    string? ContentType,
    long? Size,
    int? Width,
    int? Height,
    string? Extension,
    string? ChecksumSha256);

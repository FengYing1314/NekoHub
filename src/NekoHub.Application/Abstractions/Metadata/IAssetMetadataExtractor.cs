namespace NekoHub.Application.Abstractions.Metadata;

public interface IAssetMetadataExtractor
{
    Task<ExtractedAssetMetadata> ExtractAsync(
        Stream content,
        string? originalFileName,
        string? declaredContentType,
        CancellationToken cancellationToken = default);
}

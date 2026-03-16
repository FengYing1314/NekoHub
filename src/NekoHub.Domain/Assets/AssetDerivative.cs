namespace NekoHub.Domain.Assets;

public sealed class AssetDerivative
{
    public Guid Id { get; private set; }

    public Guid SourceAssetId { get; private set; }

    public string Kind { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public string Extension { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public int? Width { get; private set; }

    public int? Height { get; private set; }

    public string StorageProvider { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public string? PublicUrl { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private AssetDerivative()
    {
    }

    public AssetDerivative(
        Guid id,
        Guid sourceAssetId,
        string kind,
        string contentType,
        string extension,
        long size,
        int? width,
        int? height,
        string storageProvider,
        string storageKey,
        string? publicUrl)
    {
        Id = id;
        SourceAssetId = sourceAssetId;
        Kind = kind;
        ContentType = contentType;
        Extension = extension;
        Size = size;
        Width = width;
        Height = height;
        StorageProvider = storageProvider;
        StorageKey = storageKey;
        PublicUrl = publicUrl;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
}

namespace NekoHub.Domain.Assets;

public sealed class Asset
{
    public Guid Id { get; private set; }

    public AssetType Type { get; private set; }

    public AssetStatus Status { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string? StoredFileName { get; private set; }

    public string ContentType { get; private set; } = string.Empty;

    public string Extension { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public int? Width { get; private set; }

    public int? Height { get; private set; }

    public string? ChecksumSha256 { get; private set; }

    public string StorageProvider { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public string? PublicUrl { get; private set; }

    public string? Description { get; private set; }

    public string? AltText { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    private Asset()
    {
    }

    public Asset(
        Guid id,
        AssetType type,
        string originalFileName,
        string contentType,
        string extension,
        long size,
        string storageProvider,
        string storageKey,
        string? storedFileName = null,
        int? width = null,
        int? height = null,
        string? checksumSha256 = null,
        string? publicUrl = null)
    {
        Id = id;
        Type = type;
        Status = AssetStatus.Pending;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        Extension = extension;
        Size = size;
        StorageProvider = storageProvider;
        StorageKey = storageKey;
        StoredFileName = storedFileName;
        Width = width;
        Height = height;
        ChecksumSha256 = checksumSha256;
        PublicUrl = publicUrl;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void MarkReady(string? publicUrl = null)
    {
        Status = AssetStatus.Ready;
        PublicUrl = publicUrl ?? PublicUrl;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        Status = AssetStatus.Failed;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkDeleted()
    {
        Status = AssetStatus.Deleted;
        DeletedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DeletedAtUtc.Value;
    }

    public void UpdateAccessibleMetadata(string? description, string? altText)
    {
        Description = description;
        AltText = altText;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

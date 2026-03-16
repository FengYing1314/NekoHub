namespace NekoHub.Domain.Assets;

public sealed class AssetStructuredResult
{
    public Guid Id { get; private set; }

    public Guid SourceAssetId { get; private set; }

    public string Kind { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private AssetStructuredResult()
    {
    }

    public AssetStructuredResult(
        Guid id,
        Guid sourceAssetId,
        string kind,
        string payloadJson)
    {
        Id = id;
        SourceAssetId = sourceAssetId;
        Kind = kind;
        PayloadJson = payloadJson;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
}

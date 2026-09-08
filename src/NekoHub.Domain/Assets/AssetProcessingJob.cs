namespace NekoHub.Domain.Assets;

public sealed class AssetProcessingJob
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid? StorageProviderProfileId { get; set; }
    public string Kind { get; set; } = "processing";
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset AvailableAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public string? ErrorMessage { get; set; }
}

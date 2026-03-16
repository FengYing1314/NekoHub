namespace NekoHub.Api.Configuration;

public sealed class AssetApiOptions
{
    public const string SectionName = "Api:Assets";

    public long MaxUploadSizeBytes { get; init; } = 10 * 1024 * 1024;

    public string[] AllowedContentTypes { get; init; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    public int DefaultPageSize { get; init; } = 20;

    public int MaxPageSize { get; init; } = 100;
}

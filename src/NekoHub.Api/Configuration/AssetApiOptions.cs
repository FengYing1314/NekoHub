namespace NekoHub.Api.Configuration;

public sealed class AssetApiOptions
{
    public const string SectionName = "Api:Assets";
    public static readonly string[] DefaultAllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    public long MaxUploadSizeBytes { get; init; } = 10 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } = [];

    public int DefaultPageSize { get; init; } = 20;

    public int MaxPageSize { get; init; } = 100;

    public static string[] NormalizeAllowedContentTypes(IEnumerable<string>? configuredContentTypes)
    {
        var normalized = (configuredContentTypes ?? [])
            .Where(static contentType => !string.IsNullOrWhiteSpace(contentType))
            .Select(static contentType => contentType.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length > 0
            ? normalized
            : [.. DefaultAllowedContentTypes];
    }
}

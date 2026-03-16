using NekoHub.Api.Mcp.Tools.Models;

namespace NekoHub.Api.Mcp.Resources.Models;

public sealed record McpAssetDerivativesResourceView(
    Guid AssetId,
    IReadOnlyList<McpAssetDerivativeView> Derivatives);

public sealed record McpAssetStructuredResultsResourceView(
    Guid AssetId,
    IReadOnlyList<McpAssetStructuredResultView> StructuredResults);

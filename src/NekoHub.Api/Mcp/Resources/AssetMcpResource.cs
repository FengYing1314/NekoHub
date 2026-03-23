using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Resources.Models;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Resources;

public sealed class AssetMcpResource(IAssetQueryService assetQueryService) : IMcpResource
{
    private static readonly IReadOnlyList<McpResourceDescriptor> Descriptors =
    [
        new McpResourceDescriptor(
            Uri: "asset://{id}",
            Name: "asset_detail")
        {
            Title = "Asset Detail",
            Description = "Read the asset detail model by id.",
            MimeType = "application/json"
        },
        new McpResourceDescriptor(
            Uri: "asset://{id}/derivatives",
            Name: "asset_derivatives")
        {
            Title = "Asset Derivatives",
            Description = "Read file derivative outputs for the specified asset id.",
            MimeType = "application/json"
        },
        new McpResourceDescriptor(
            Uri: "asset://{id}/structured-results",
            Name: "asset_structured_results")
        {
            Title = "Asset Structured Results",
            Description = "Read structured analysis outputs for the specified asset id.",
            MimeType = "application/json"
        }
    ];

    public Task<IReadOnlyList<McpResourceDescriptor>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Descriptors);
    }

    public bool CanHandle(Uri resourceUri)
    {
        return string.Equals(resourceUri.Scheme, "asset", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<McpResourceReadResult> ReadAsync(Uri resourceUri, CancellationToken cancellationToken)
    {
        var assetId = ParseAssetId(resourceUri);
        var asset = await assetQueryService.GetByIdAsync(assetId, cancellationToken);
        var assetView = McpAssetToolModelMapper.ToView(asset);
        var resourcePath = resourceUri.AbsolutePath.Trim('/');

        return resourcePath switch
        {
            "" => new McpResourceReadResult(resourceUri.ToString(), assetView),
            "derivatives" => new McpResourceReadResult(
                resourceUri.ToString(),
                new McpAssetDerivativesResourceView(asset.Id, assetView.Derivatives)),
            "structured-results" => new McpResourceReadResult(
                resourceUri.ToString(),
                new McpAssetStructuredResultsResourceView(asset.Id, assetView.StructuredResults)),
            _ => throw new NotFoundException(
                "resource_not_found",
                $"Resource '{resourceUri}' was not found.")
        };
    }

    private static Guid ParseAssetId(Uri resourceUri)
    {
        if (!Guid.TryParse(resourceUri.Host, out var assetId))
        {
            throw new ValidationException(
                "resource_uri_invalid",
                $"Resource uri '{resourceUri}' has an invalid asset id.");
        }

        return assetId;
    }
}

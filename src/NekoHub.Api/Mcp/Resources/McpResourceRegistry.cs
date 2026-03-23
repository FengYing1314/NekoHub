using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Resources;

public sealed class McpResourceRegistry(IEnumerable<IMcpResource> resources)
{
    private readonly IReadOnlyList<IMcpResource> _resources = resources.ToList();

    public async Task<IReadOnlyList<McpResourceDescriptor>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        var definitions = new List<McpResourceDescriptor>();
        foreach (var resource in _resources)
        {
            var descriptors = await resource.ListAsync(cancellationToken);
            definitions.AddRange(descriptors);
        }

        return definitions
            .OrderBy(static descriptor => descriptor.Uri, StringComparer.Ordinal)
            .ToList();
    }

    public bool TryResolve(Uri resourceUri, out IMcpResource? resource)
    {
        resource = _resources.FirstOrDefault(candidate => candidate.CanHandle(resourceUri));
        return resource is not null;
    }
}

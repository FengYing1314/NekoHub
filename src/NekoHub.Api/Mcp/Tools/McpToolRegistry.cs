using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Tools;

public sealed class McpToolRegistry(IEnumerable<IMcpTool> tools)
{
    private readonly IReadOnlyDictionary<string, IMcpTool> _tools = tools
        .ToDictionary(static tool => tool.Definition.Name, StringComparer.Ordinal);

    public IReadOnlyList<McpToolDescriptor> GetDefinitions()
    {
        return _tools.Values
            .OrderBy(static tool => tool.Definition.Name, StringComparer.Ordinal)
            .Select(static tool => tool.Definition)
            .ToList();
    }

    public bool TryGet(string toolName, out IMcpTool? tool)
    {
        return _tools.TryGetValue(toolName, out tool);
    }
}

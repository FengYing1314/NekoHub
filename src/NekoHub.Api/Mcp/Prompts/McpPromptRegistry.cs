using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Prompts;

public sealed class McpPromptRegistry(IEnumerable<IMcpPrompt> prompts)
{
    private readonly IReadOnlyDictionary<string, IMcpPrompt> _prompts = prompts
        .ToDictionary(static prompt => prompt.Definition.Name, StringComparer.Ordinal);

    public IReadOnlyList<McpPromptDescriptor> GetDefinitions()
    {
        return _prompts.Values
            .OrderBy(static prompt => prompt.Definition.Name, StringComparer.Ordinal)
            .Select(static prompt => prompt.Definition)
            .ToList();
    }

    public bool TryGet(string promptName, out IMcpPrompt? prompt)
    {
        return _prompts.TryGetValue(promptName, out prompt);
    }
}

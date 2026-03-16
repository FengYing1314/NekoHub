namespace NekoHub.Api.Mcp.Protocol;

public static class McpProtocolConstants
{
    public const string JsonRpcVersion = "2.0";
    public const string LatestProtocolVersion = "2025-11-25";
    public const string ProtocolVersionHeader = "MCP-Protocol-Version";

    private static readonly HashSet<string> SupportedVersions = new(StringComparer.Ordinal)
    {
        LatestProtocolVersion,
        "2025-06-18",
        "2025-03-26"
    };

    public static bool IsSupportedProtocolVersion(string? protocolVersion)
    {
        return !string.IsNullOrWhiteSpace(protocolVersion) && SupportedVersions.Contains(protocolVersion);
    }
}

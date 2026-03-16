using System.Threading.Channels;

namespace NekoHub.Api.Mcp;

public interface IMcpSessionManager
{
    McpSseSession CreateSession();

    bool TryGetWriter(string sessionId, out ChannelWriter<string>? writer);

    bool RemoveSession(string sessionId);
}

public sealed record McpSseSession(
    string SessionId,
    ChannelReader<string> Reader);

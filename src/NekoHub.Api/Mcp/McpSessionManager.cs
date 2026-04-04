using System.Collections.Concurrent;
using System.Threading.Channels;

namespace NekoHub.Api.Mcp;

public sealed class McpSessionManager : IMcpSessionManager
{
    private readonly ConcurrentDictionary<string, Channel<string>> _sessions = new(StringComparer.Ordinal);

    public McpSseSession CreateSession()
    {
        while (true)
        {
            var sessionId = Guid.CreateVersion7().ToString("N");
            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            if (_sessions.TryAdd(sessionId, channel))
            {
                return new McpSseSession(sessionId, channel.Reader);
            }
        }
    }

    public bool TryGetWriter(string sessionId, out ChannelWriter<string>? writer)
    {
        writer = null;

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        if (!_sessions.TryGetValue(sessionId.Trim(), out var channel))
        {
            return false;
        }

        writer = channel.Writer;
        return true;
    }

    public bool RemoveSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        if (!_sessions.TryRemove(sessionId.Trim(), out var channel))
        {
            return false;
        }

        channel.Writer.TryComplete();
        return true;
    }
}

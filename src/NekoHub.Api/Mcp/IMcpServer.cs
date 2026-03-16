namespace NekoHub.Api.Mcp;

public interface IMcpServer
{
    McpHttpResponse HandleGet();

    Task<McpHttpResponse> HandlePostAsync(HttpRequest request, CancellationToken cancellationToken);
}

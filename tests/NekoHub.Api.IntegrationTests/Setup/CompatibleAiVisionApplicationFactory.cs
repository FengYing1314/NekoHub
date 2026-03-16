using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NekoHub.Application.Abstractions.Ai;
using NekoHub.Infrastructure.Ai;

namespace NekoHub.Api.IntegrationTests.Setup;

public sealed class CompatibleAiVisionApplicationFactory : NekoHubApplicationFactory
{
    public SequencedAiVisionHttpMessageHandler VisionHandler { get; } = new();
    public override bool AutoAuthenticateClient => false;

    protected override IDictionary<string, string?> CreateInMemoryConfiguration()
    {
        var configuration = base.CreateInMemoryConfiguration();
        configuration["Auth:ApiKey:Enabled"] = "true";
        configuration["Auth:ApiKey:Keys:0"] = NekoHubApiKeyApplicationFactory.TestApiKey;
        return configuration;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiVisionClient>();
            services.AddHttpClient<IAiVisionClient, OpenAiVisionClient>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                })
                .ConfigurePrimaryHttpMessageHandler(() => VisionHandler);
        });
    }
}

public sealed class SequencedAiVisionHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = new();
    private readonly object _sync = new();

    public List<string> RequestBodies { get; } = [];

    public void EnqueueJson(object payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(payload)
        }));
    }

    public void EnqueueStreaming(params string[] payloads)
    {
        var builder = new StringBuilder();
        foreach (var payload in payloads)
        {
            builder.Append("data: ");
            builder.Append(payload);
            builder.Append("\n\n");
        }

        builder.Append("data: [DONE]\n\n");

        Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(builder.ToString(), Encoding.UTF8, "text/event-stream")
        }));
    }

    public void Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        lock (_sync)
        {
            _responses.Enqueue(responseFactory);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }
        else
        {
            RequestBodies.Add(string.Empty);
        }

        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory;
        lock (_sync)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued AI vision HTTP response is available for this request.");
            }

            responseFactory = _responses.Dequeue();
        }

        return await responseFactory(request, cancellationToken);
    }
}

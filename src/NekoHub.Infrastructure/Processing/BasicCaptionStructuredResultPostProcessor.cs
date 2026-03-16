using System.Text.Json;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Processing;

public sealed class BasicCaptionStructuredResultPostProcessor(
    IAssetStructuredResultRepository structuredResultRepository) : IAssetPostProcessor
{
    public string Name => "basic_caption";

    public int Order => 200;

    public async Task ProcessAsync(
        AssetCreatedProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        if (!IsImage(context.ContentType))
        {
            return;
        }

        var existing = await structuredResultRepository.GetBySourceAndKindAsync(
            context.AssetId,
            AssetStructuredResultKinds.BasicCaption,
            cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var captionText = BuildCaption(context);
        var payloadJson = JsonSerializer.Serialize(new
        {
            caption = captionText,
            generator = "stub.basic_caption.v1"
        });

        var result = new AssetStructuredResult(
            id: Guid.CreateVersion7(),
            sourceAssetId: context.AssetId,
            kind: AssetStructuredResultKinds.BasicCaption,
            payloadJson: payloadJson);

        await structuredResultRepository.AddAsync(result, cancellationToken);
        await structuredResultRepository.SaveChangesAsync(cancellationToken);
    }

    private static string BuildCaption(AssetCreatedProcessingContext context)
    {
        if (context.Width is > 0 && context.Height is > 0)
        {
            return $"Image {context.Width}x{context.Height}, type {context.ContentType}.";
        }

        return $"Image asset, type {context.ContentType}.";
    }

    private static bool IsImage(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
               && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}

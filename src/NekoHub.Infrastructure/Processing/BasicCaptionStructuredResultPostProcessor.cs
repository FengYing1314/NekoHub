using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NekoHub.Application.Abstractions.Ai;
using NekoHub.Application.Ai.Services;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Assets.Services;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Processing;

public sealed class BasicCaptionStructuredResultPostProcessor(
    IAssetStructuredResultRepository structuredResultRepository,
    IAssetRepository assetRepository,
    IAssetDerivativeRepository assetDerivativeRepository,
    IAssetStorageTargetSelector assetStorageTargetSelector,
    IAiProviderProfileService aiProviderProfileService,
    IAiVisionClient aiVisionClient,
    ILogger<BasicCaptionStructuredResultPostProcessor> logger) : IAssetPostProcessor
{
    private const string CaptionUserPrompt = "Please analyze this image and return a concise factual caption.";

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
            await BackfillAssetCaptionAsync(context.AssetId, TryExtractCaption(existing.PayloadJson), cancellationToken);
            return;
        }

        var runtimeProfile = await aiProviderProfileService.GetActiveRuntimeProfileAsync(cancellationToken);
        if (runtimeProfile is null)
        {
            logger.LogWarning(
                "Skipping basic caption generation because no active AI provider profile is configured. AssetId={AssetId}",
                context.AssetId);
            return;
        }

        var imageDataUrl = await CreateCaptionImageDataUrlAsync(context.AssetId, cancellationToken);
        var caption = await aiVisionClient.GenerateAsync(
            new AiVisionRequest(
                ApiBaseUrl: runtimeProfile.ApiBaseUrl,
                ApiKey: runtimeProfile.ApiKey,
                ModelName: runtimeProfile.ModelName,
                SystemPrompt: runtimeProfile.DefaultSystemPrompt,
                UserPrompt: CaptionUserPrompt,
                ImageDataUrl: imageDataUrl),
            cancellationToken);

        var payloadJson = JsonSerializer.Serialize(new
        {
            caption = caption.Caption,
            generator = caption.ModelName
        });

        var result = new AssetStructuredResult(
            id: Guid.CreateVersion7(),
            sourceAssetId: context.AssetId,
            kind: AssetStructuredResultKinds.BasicCaption,
            payloadJson: payloadJson);

        await structuredResultRepository.AddAsync(result, cancellationToken);
        try
        {
            await structuredResultRepository.SaveChangesAsync(cancellationToken);
            await BackfillAssetCaptionAsync(context.AssetId, caption.Caption, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, "IX_AssetStructuredResults_SourceAssetId_Kind"))
        {
            logger.LogInformation(
                "Structured result already exists due to concurrent processing. AssetId={AssetId}, Kind={Kind}",
                context.AssetId,
                AssetStructuredResultKinds.BasicCaption);

            var latest = await structuredResultRepository.GetBySourceAndKindAsync(
                context.AssetId,
                AssetStructuredResultKinds.BasicCaption,
                cancellationToken);
            await BackfillAssetCaptionAsync(context.AssetId, TryExtractCaption(latest?.PayloadJson), cancellationToken);
        }
    }

    private async Task<string> CreateCaptionImageDataUrlAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var asset = await assetRepository.GetByIdAsync(assetId, cancellationToken);
        if (asset is null)
        {
            throw new InvalidOperationException($"Asset '{assetId}' was not found for basic caption generation.");
        }

        var thumbnail = await assetDerivativeRepository.GetBySourceAndKindAsync(
            assetId,
            AssetDerivativeKinds.Thumbnail256,
            cancellationToken);

        if (thumbnail is not null)
        {
            return await CreateDataUrlFromStorageAsync(
                asset.StorageProviderProfileId,
                thumbnail.StorageProvider,
                thumbnail.StorageKey,
                thumbnail.ContentType,
                cancellationToken);
        }

        return await CreateDataUrlFromStorageAsync(
            asset.StorageProviderProfileId,
            asset.StorageProvider,
            asset.StorageKey,
            asset.ContentType,
            cancellationToken);
    }

    private async Task<string> CreateDataUrlFromStorageAsync(
        Guid? storageProviderProfileId,
        string storageProvider,
        string storageKey,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var storageLease = await assetStorageTargetSelector.ResolveReadTargetAsync(
            storageProviderProfileId,
            storageProvider,
            cancellationToken);
        await using var sourceStream = await storageLease.Storage.OpenReadAsync(storageKey, cancellationToken);
        if (sourceStream is null)
        {
            throw new InvalidOperationException($"Asset content '{storageKey}' could not be opened for basic caption generation.");
        }

        return await CreateDataUrlAsync(sourceStream, contentType, cancellationToken);
    }

    private static async Task<string> CreateDataUrlAsync(
        Stream sourceStream,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await sourceStream.CopyToAsync(buffer, cancellationToken);

        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();

        var base64 = buffer.TryGetBuffer(out var segment)
            ? Convert.ToBase64String(segment.Array!, segment.Offset, checked((int)buffer.Length))
            : Convert.ToBase64String(buffer.ToArray());

        return $"data:{normalizedContentType};base64,{base64}";
    }

    private static bool IsImage(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
               && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task BackfillAssetCaptionAsync(Guid assetId, string? caption, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return;
        }

        var asset = await assetRepository.GetByIdAsync(assetId, cancellationToken);
        if (asset is null)
        {
            throw new InvalidOperationException($"Asset '{assetId}' was not found for basic caption generation.");
        }

        var resolvedDescription = string.IsNullOrWhiteSpace(asset.Description)
            ? caption.Trim()
            : asset.Description;
        var resolvedAltText = string.IsNullOrWhiteSpace(asset.AltText)
            ? caption.Trim()
            : asset.AltText;

        if (resolvedDescription == asset.Description && resolvedAltText == asset.AltText)
        {
            return;
        }

        asset.UpdateAccessibleMetadata(resolvedDescription, resolvedAltText);
        await assetRepository.SaveChangesAsync(cancellationToken);
    }

    private static string? TryExtractCaption(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<BasicCaptionPayload>(payloadJson);
            return string.IsNullOrWhiteSpace(payload?.Caption)
                ? null
                : payload.Caption.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception, string constraintName)
    {
        return exception.InnerException is PostgresException postgresException
               && string.Equals(postgresException.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
               && string.Equals(postgresException.ConstraintName, constraintName, StringComparison.Ordinal);
    }

    private sealed record BasicCaptionPayload([property: JsonPropertyName("caption")] string? Caption);
}

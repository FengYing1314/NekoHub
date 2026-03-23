using System.Text.Json;
using Microsoft.Extensions.Options;
using NekoHub.Api.Configuration;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Commands;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Tools;

public sealed class UploadAssetMcpTool(
    IAssetCommandService assetCommandService,
    IAssetQueryService assetQueryService,
    IOptions<AssetApiOptions> assetApiOptions) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "upload_asset",
        InputSchema: McpAssetToolSchemas.UploadAssetInput)
    {
        Title = "Upload Asset",
        Description =
            "Upload an image asset using base64 payload and return the asset detail read model including derivatives and structured results.",
        OutputSchema = McpAssetToolSchemas.AssetDetail,
        Annotations = new McpToolAnnotations
        {
            ReadOnlyHint = false,
            DestructiveHint = false,
            IdempotentHint = false,
            OpenWorldHint = false
        }
    };

    public async Task<McpToolInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        var input = McpToolArgumentParser.ParseRequired<Arguments>(arguments, Definition.Name);
        ValidateInput(input);

        byte[] contentBytes;
        try
        {
            contentBytes = Convert.FromBase64String(input.ContentBase64!);
        }
        catch (FormatException)
        {
            throw new ValidationException("asset_base64_invalid", "Argument 'contentBase64' must be valid base64.");
        }

        var options = assetApiOptions.Value;
        if (contentBytes.Length <= 0)
        {
            throw new ValidationException("asset_file_empty", "Uploaded file is empty.");
        }

        if (contentBytes.LongLength > options.MaxUploadSizeBytes)
        {
            throw new ValidationException(
                "asset_file_too_large",
                $"File size exceeds limit {options.MaxUploadSizeBytes} bytes.");
        }

        await using var contentStream = new MemoryStream(contentBytes, writable: false);
        var uploaded = await assetCommandService.UploadAsync(
            new UploadAssetCommand(
                Content: contentStream,
                OriginalFileName: input.FileName!.Trim(),
                DeclaredContentType: input.ContentType!.Trim(),
                DeclaredSize: contentBytes.LongLength,
                Description: input.Description,
                AltText: input.AltText),
            cancellationToken);

        var details = await assetQueryService.GetByIdAsync(uploaded.Id, cancellationToken);
        return new McpToolInvocationResult(McpAssetToolModelMapper.ToView(details));
    }

    private void ValidateInput(Arguments input)
    {
        var fileName = input.FileName?.Trim();
        var contentType = input.ContentType?.Trim();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ValidationException("asset_filename_invalid", "Argument 'fileName' is required.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ValidationException("asset_content_type_missing", "Argument 'contentType' is required.");
        }

        if (string.IsNullOrWhiteSpace(input.ContentBase64))
        {
            throw new ValidationException("asset_file_required", "Argument 'contentBase64' is required.");
        }

        if (input.Description is { Length: > 1000 })
        {
            throw new ValidationException("asset_description_too_long", "Description must be 1000 characters or fewer.");
        }

        if (input.AltText is { Length: > 1000 })
        {
            throw new ValidationException("asset_alt_text_too_long", "Alt text must be 1000 characters or fewer.");
        }

        var options = assetApiOptions.Value;
        var isAllowedType = options.AllowedContentTypes
            .Contains(contentType, StringComparer.OrdinalIgnoreCase);
        if (!isAllowedType)
        {
            throw new ValidationException("asset_content_type_not_allowed", $"Content type '{contentType}' is not allowed.");
        }
    }

    private sealed record Arguments(
        string? FileName,
        string? ContentType,
        string? ContentBase64,
        string? Description,
        string? AltText);
}

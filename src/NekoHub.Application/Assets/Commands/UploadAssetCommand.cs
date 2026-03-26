namespace NekoHub.Application.Assets.Commands;

public sealed record UploadAssetCommand(
    Stream Content,
    string OriginalFileName,
    string? DeclaredContentType,
    long DeclaredSize,
    string? Description,
    string? AltText,
    bool IsPublic = true);

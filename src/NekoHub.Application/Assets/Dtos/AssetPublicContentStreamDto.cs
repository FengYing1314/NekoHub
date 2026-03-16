namespace NekoHub.Application.Assets.Dtos;

public sealed record AssetPublicContentStreamDto(
    Stream Content,
    string ContentType);

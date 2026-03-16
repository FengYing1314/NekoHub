namespace NekoHub.Application.Assets.Dtos;

public sealed record AssetContentRedirectDto(
    Guid Id,
    string RedirectUrl,
    bool PreserveMethod);

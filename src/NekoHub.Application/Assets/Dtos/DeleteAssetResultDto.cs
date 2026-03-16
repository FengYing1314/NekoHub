namespace NekoHub.Application.Assets.Dtos;

public sealed record DeleteAssetResultDto(
    Guid Id,
    string Status,
    DateTimeOffset DeletedAtUtc);

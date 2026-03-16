namespace NekoHub.Application.Storage.Dtos;

public sealed record DeleteStorageProviderProfileResultDto(
    Guid Id,
    bool WasDefault,
    string Status,
    DateTimeOffset DeletedAtUtc);

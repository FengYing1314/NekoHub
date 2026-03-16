namespace NekoHub.Application.Storage.Queries.Dtos;

public sealed record StorageProviderOverviewQueryDto(
    IReadOnlyList<StorageProviderProfileQueryDto> Profiles,
    StorageProviderProfileQueryDto? DefaultProfile,
    StorageProviderProfileQueryDto? DefaultWriteProfile,
    StorageRuntimeSummaryQueryDto Runtime,
    StorageRuntimeAlignmentStatusQueryDto Alignment);

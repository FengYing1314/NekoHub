namespace NekoHub.Api.Contracts.Responses;

public sealed record StorageProviderOverviewResponse(
    IReadOnlyList<StorageProviderProfileResponse> Profiles,
    StorageProviderProfileResponse? DefaultProfile,
    StorageProviderProfileResponse? DefaultWriteProfile,
    StorageRuntimeSummaryResponse Runtime,
    StorageRuntimeAlignmentStatusResponse Alignment);

namespace NekoHub.Application.Storage.Queries.Dtos;

public sealed record StorageRuntimeAlignmentStatusQueryDto(
    string RuntimeSelectionSource,
    bool HasDefaultProfile,
    bool? IsDefaultProfileEnabled,
    bool? ProviderTypeMatchesDefaultProfile,
    string Code,
    string Message);

namespace NekoHub.Api.Contracts.Responses;

public sealed record StorageRuntimeAlignmentStatusResponse(
    string RuntimeSelectionSource,
    bool HasDefaultProfile,
    bool? IsDefaultProfileEnabled,
    bool? ProviderTypeMatchesDefaultProfile,
    string Code,
    string Message);

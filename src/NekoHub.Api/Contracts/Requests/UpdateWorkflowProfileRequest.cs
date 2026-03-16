namespace NekoHub.Api.Contracts.Requests;

public sealed class UpdateWorkflowProfileRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public bool? IsAutoRun { get; init; }

    public string? GraphJson { get; init; }
}

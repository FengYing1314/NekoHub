namespace NekoHub.Domain.Skills;

public sealed class SkillExecutionStepResult
{
    public Guid Id { get; private set; }

    public Guid SkillExecutionId { get; private set; }

    public string StepName { get; private set; } = string.Empty;

    public bool Succeeded { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset CompletedAtUtc { get; private set; }

    private SkillExecutionStepResult()
    {
    }

    public SkillExecutionStepResult(
        Guid id,
        Guid skillExecutionId,
        string stepName,
        bool succeeded,
        string? errorMessage,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        if (completedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("CompletedAtUtc must be greater than or equal to StartedAtUtc.");
        }

        Id = id;
        SkillExecutionId = skillExecutionId;
        StepName = stepName.Trim();
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
    }
}

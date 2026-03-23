namespace NekoHub.Domain.Skills;

public sealed class SkillExecution
{
    public Guid Id { get; private set; }

    public Guid SourceAssetId { get; private set; }

    public string SkillName { get; private set; } = string.Empty;

    public string TriggerSource { get; private set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset CompletedAtUtc { get; private set; }

    public bool Succeeded { get; private set; }

    private SkillExecution()
    {
    }

    public SkillExecution(
        Guid id,
        Guid sourceAssetId,
        string skillName,
        string triggerSource,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool succeeded)
    {
        if (completedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("CompletedAtUtc must be greater than or equal to StartedAtUtc.");
        }

        Id = id;
        SourceAssetId = sourceAssetId;
        SkillName = skillName.Trim();
        TriggerSource = triggerSource.Trim();
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Succeeded = succeeded;
    }
}

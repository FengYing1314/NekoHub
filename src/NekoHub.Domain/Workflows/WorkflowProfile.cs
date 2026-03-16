namespace NekoHub.Domain.Workflows;

public sealed class WorkflowProfile
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 1000;

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsAutoRun { get; private set; }

    public string GraphJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private WorkflowProfile()
    {
    }

    public WorkflowProfile(
        Guid id,
        string name,
        string? description,
        bool isAutoRun,
        string graphJson)
    {
        Id = id;
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        IsAutoRun = isAutoRun;
        GraphJson = NormalizeGraphJson(graphJson);
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void UpdateDefinition(string name, string? description, string graphJson)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        GraphJson = NormalizeGraphJson(graphJson);
        Touch();
    }

    public void SetAutoRun(bool isAutoRun)
    {
        if (IsAutoRun == isAutoRun)
        {
            return;
        }

        IsAutoRun = isAutoRun;
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("name is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > NameMaxLength)
        {
            throw new ArgumentException($"name must be {NameMaxLength} characters or fewer.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > DescriptionMaxLength)
        {
            throw new ArgumentException($"description must be {DescriptionMaxLength} characters or fewer.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeGraphJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("graphJson is required.", nameof(value));
        }

        return value.Trim();
    }
}

using NekoHub.Application.Abstractions.Processing;

namespace NekoHub.Application.Abstractions.Skills;

public sealed class SkillExecutionContext
{
    public SkillExecutionContext(AssetCreatedProcessingContext asset, string triggerSource)
    {
        Asset = asset;
        TriggerSource = NormalizeTriggerSource(triggerSource);
    }

    public AssetCreatedProcessingContext Asset { get; }

    public string TriggerSource { get; }

    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    private static string NormalizeTriggerSource(string triggerSource)
    {
        if (string.IsNullOrWhiteSpace(triggerSource))
        {
            throw new ArgumentException("Trigger source is required.", nameof(triggerSource));
        }

        return triggerSource.Trim().ToLowerInvariant();
    }
}

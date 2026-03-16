using System.Text.Json.Nodes;
using NekoHub.Application.Abstractions.Processing;

namespace NekoHub.Application.Abstractions.Skills;

public sealed class SkillExecutionContext
{
    public SkillExecutionContext(
        AssetCreatedProcessingContext asset,
        string triggerSource,
        JsonObject? parameters = null)
    {
        Asset = asset;
        TriggerSource = NormalizeTriggerSource(triggerSource);
        Parameters = parameters?.DeepClone() as JsonObject;
    }

    public AssetCreatedProcessingContext Asset { get; }

    public string TriggerSource { get; }

    public JsonObject? Parameters { get; }

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

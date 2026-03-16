using System.Text.Json;

namespace NekoHub.Api.Contracts.Requests;

public sealed class RunAssetSkillRequest
{
    public JsonElement? Parameters { get; init; }
}

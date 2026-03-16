namespace NekoHub.Application.Workflows.Parsing;

public interface IWorkflowGraphParser
{
    IReadOnlyList<WorkflowSkillNodeDefinition> ExtractSkills(string graphJson);
}

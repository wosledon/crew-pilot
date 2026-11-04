using CrewPilot.Core.Services;
using CrewPilot.Core.WorkItems;

namespace CrewPilot.Agents.Execution;

/// <summary>系统提示词模板变量替换（PRD 5.2）：{{title}} / {{requirement}} / {{acceptanceCriteria}} 等。</summary>
public static class PromptTemplate
{
    public static string Render(string? template, WorkItem workItem)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        var description = workItem.Description ?? string.Empty;
        return template
            .Replace("{{title}}", workItem.Title, StringComparison.Ordinal)
            .Replace("{{description}}", description, StringComparison.Ordinal)
            .Replace("{{requirement}}", description, StringComparison.Ordinal)
            .Replace("{{acceptanceCriteria}}", workItem.AcceptanceCriteria ?? string.Empty,
                StringComparison.Ordinal)
            .Replace("{{type}}", TypeOf(workItem), StringComparison.Ordinal)
            .Replace("{{status}}", StatusOf(workItem), StringComparison.Ordinal)
            .Replace("{{priority}}", workItem.Priority.ToString().ToLowerInvariant(),
                StringComparison.Ordinal)
            .Replace("{{workItemId}}", workItem.Id.ToString(), StringComparison.Ordinal);
    }

    public static string TypeOf(WorkItem workItem) => workItem switch
    {
        Requirement => "requirement",
        Defect => "defect",
        TaskItem => "task",
        _ => "workitem",
    };

    public static string StatusOf(WorkItem workItem) => workItem switch
    {
        Requirement requirement => requirement.Status.ToString(),
        Defect defect => defect.Status.ToString(),
        TaskItem task => WorkItemStateMachine.ToStatus(task),
        _ => string.Empty,
    };
}

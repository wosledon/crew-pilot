namespace CrewPilot.Core.Services;

using CrewPilot.Core.WorkItems;

/// <summary>
/// 工作项状态机：禁止在 Controller 里直接改状态，一律经此状态机流转。
/// </summary>
public static class WorkItemStateMachine
{
    public static bool CanTransition(Requirement requirement, RequirementStatus to) =>
        (requirement.Status, to) switch
        {
            (RequirementStatus.Backlog, RequirementStatus.Ready or RequirementStatus.Rejected) => true,
            (RequirementStatus.Ready, RequirementStatus.InProgress or RequirementStatus.Backlog or RequirementStatus.Rejected) => true,
            (RequirementStatus.InProgress, RequirementStatus.InReview or RequirementStatus.Ready) => true,
            (RequirementStatus.InReview, RequirementStatus.Done or RequirementStatus.InProgress) => true,
            _ => false,
        };

    public static bool CanTransition(Defect defect, DefectStatus to) =>
        (defect.Status, to) switch
        {
            (DefectStatus.New, DefectStatus.Confirmed or DefectStatus.NotADefect) => true,
            (DefectStatus.Confirmed, DefectStatus.Fixing or DefectStatus.Deferred) => true,
            (DefectStatus.Fixing, DefectStatus.PendingVerification or DefectStatus.Confirmed) => true,
            (DefectStatus.PendingVerification, DefectStatus.Closed or DefectStatus.Fixing) => true,
            (DefectStatus.Deferred, DefectStatus.Confirmed) => true,
            _ => false,
        };

    /// <summary>需求状态流转；非法流转抛 <see cref="InvalidOperationException"/>。</summary>
    public static void Transition(Requirement requirement, RequirementStatus to)
    {
        if (!CanTransition(requirement, to))
        {
            throw new InvalidOperationException(
                $"需求不允许从 {requirement.Status} 流转到 {to}");
        }

        requirement.Status = to;
    }

    /// <summary>缺陷状态流转；非法流转抛 <see cref="InvalidOperationException"/>。</summary>
    public static void Transition(Defect defect, DefectStatus to)
    {
        if (!CanTransition(defect, to))
        {
            throw new InvalidOperationException(
                $"缺陷不允许从 {defect.Status} 流转到 {to}");
        }

        defect.Status = to;
    }
}

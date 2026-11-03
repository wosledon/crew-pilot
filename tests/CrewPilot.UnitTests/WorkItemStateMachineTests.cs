using CrewPilot.Core.Services;
using CrewPilot.Core.WorkItems;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public class WorkItemStateMachineTests
{
    [Fact]
    public void Requirement_CanTransition_AlongHappyPath()
    {
        var requirement = new Requirement();

        WorkItemStateMachine.CanTransition(requirement, RequirementStatus.Ready).Should().BeTrue();
        WorkItemStateMachine.Transition(requirement, RequirementStatus.Ready);

        WorkItemStateMachine.CanTransition(requirement, RequirementStatus.InProgress).Should().BeTrue();
        WorkItemStateMachine.Transition(requirement, RequirementStatus.InProgress);

        WorkItemStateMachine.CanTransition(requirement, RequirementStatus.InReview).Should().BeTrue();
        WorkItemStateMachine.Transition(requirement, RequirementStatus.InReview);

        WorkItemStateMachine.CanTransition(requirement, RequirementStatus.Done).Should().BeTrue();
        WorkItemStateMachine.Transition(requirement, RequirementStatus.Done);

        requirement.Status.Should().Be(RequirementStatus.Done);
    }

    [Fact]
    public void Requirement_CannotSkipStages()
    {
        var requirement = new Requirement();

        WorkItemStateMachine.CanTransition(requirement, RequirementStatus.InProgress).Should().BeFalse();

        var act = () => WorkItemStateMachine.Transition(requirement, RequirementStatus.InProgress);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Requirement_InReviewCanBeRejectedBackToInProgress()
    {
        var requirement = new Requirement { Status = RequirementStatus.InReview };

        WorkItemStateMachine.CanTransition(requirement, RequirementStatus.InProgress).Should().BeTrue();
        WorkItemStateMachine.Transition(requirement, RequirementStatus.InProgress);

        requirement.Status.Should().Be(RequirementStatus.InProgress);
    }

    [Fact]
    public void Requirement_CanBeRejectedFromBacklog()
    {
        var requirement = new Requirement();

        WorkItemStateMachine.CanTransition(requirement, RequirementStatus.Rejected).Should().BeTrue();
        WorkItemStateMachine.Transition(requirement, RequirementStatus.Rejected);

        // 已拒绝为终态
        WorkItemStateMachine.CanTransition(requirement, RequirementStatus.Backlog).Should().BeFalse();
    }

    [Fact]
    public void Defect_CanTransition_AlongHappyPath()
    {
        var defect = new Defect();

        WorkItemStateMachine.CanTransition(defect, DefectStatus.Confirmed).Should().BeTrue();
        WorkItemStateMachine.Transition(defect, DefectStatus.Confirmed);

        WorkItemStateMachine.CanTransition(defect, DefectStatus.Fixing).Should().BeTrue();
        WorkItemStateMachine.Transition(defect, DefectStatus.Fixing);

        WorkItemStateMachine.CanTransition(defect, DefectStatus.PendingVerification).Should().BeTrue();
        WorkItemStateMachine.Transition(defect, DefectStatus.PendingVerification);

        WorkItemStateMachine.CanTransition(defect, DefectStatus.Closed).Should().BeTrue();
        WorkItemStateMachine.Transition(defect, DefectStatus.Closed);

        defect.Status.Should().Be(DefectStatus.Closed);
    }

    [Fact]
    public void Defect_PendingVerificationCanReturnToFixing()
    {
        var defect = new Defect { Status = DefectStatus.PendingVerification };

        WorkItemStateMachine.CanTransition(defect, DefectStatus.Fixing).Should().BeTrue();
        WorkItemStateMachine.Transition(defect, DefectStatus.Fixing);

        defect.Status.Should().Be(DefectStatus.Fixing);
    }

    [Fact]
    public void Defect_ClosedIsTerminal()
    {
        var defect = new Defect { Status = DefectStatus.Closed };

        WorkItemStateMachine.CanTransition(defect, DefectStatus.Fixing).Should().BeFalse();
        WorkItemStateMachine.CanTransition(defect, DefectStatus.Confirmed).Should().BeFalse();
    }
}

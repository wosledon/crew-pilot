using CrewPilot.Agents.Execution;
using CrewPilot.Core.Projects;
using CrewPilot.Core.WorkItems;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public sealed class PromptTemplateTests
{
    private static Requirement CreateRequirement() => new()
    {
        ProjectId = Guid.NewGuid(),
        Title = "实现登录",
        Description = "支持邮箱 + 验证码登录",
        AcceptanceCriteria = "- 可登录\n- 错误提示",
        Priority = Priority.P1,
        Status = RequirementStatus.InProgress,
    };

    [Fact]
    public void Render_ReplacesKnownVariables()
    {
        var requirement = CreateRequirement();
        var template =
            "标题={{title}}；需求={{requirement}}；验收={{acceptanceCriteria}}；" +
            "类型={{type}}；状态={{status}}；优先级={{priority}}；ID={{workItemId}}";

        var result = PromptTemplate.Render(template, requirement);

        result.Should().Contain("标题=实现登录");
        result.Should().Contain("需求=支持邮箱 + 验证码登录");
        result.Should().Contain("验收=- 可登录");
        result.Should().Contain("类型=requirement");
        result.Should().Contain("状态=InProgress");
        result.Should().Contain("优先级=p1");
        result.Should().Contain($"ID={requirement.Id}");
    }

    [Fact]
    public void Render_LeavesUnknownPlaceholdersUntouched()
    {
        var result = PromptTemplate.Render("{{unknown}}", CreateRequirement());
        result.Should().Be("{{unknown}}");
    }

    [Fact]
    public void Render_HandlesNullTemplate()
    {
        PromptTemplate.Render(null, CreateRequirement()).Should().BeEmpty();
    }

    [Fact]
    public void TypeAndStatus_ForDefectAndTask()
    {
        var defect = new Defect
        {
            ProjectId = Guid.NewGuid(),
            Title = "缺陷",
            Status = DefectStatus.Fixing,
        };
        PromptTemplate.TypeOf(defect).Should().Be("defect");
        PromptTemplate.StatusOf(defect).Should().Be("Fixing");

        var task = new TaskItem { ProjectId = Guid.NewGuid(), Title = "任务", IsDone = true };
        PromptTemplate.TypeOf(task).Should().Be("task");
        PromptTemplate.StatusOf(task).Should().Be("done");
    }
}

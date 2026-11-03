using CrewPilot.Core.WorkItems;
using CrewPilot.Core.WorkLogs;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public class WorkItemHoursTests
{
    [Fact]
    public void LoggedHours_SumsAllWorkLogs()
    {
        var requirement = new Requirement { Title = "搭建项目骨架" };
        requirement.WorkLogs.Add(new WorkLog { WorkItemId = requirement.Id, Hours = 1.5m });
        requirement.WorkLogs.Add(new WorkLog { WorkItemId = requirement.Id, Hours = 2m });

        requirement.LoggedHours.Should().Be(3.5m);
    }

    [Fact]
    public void LoggedHours_IsZeroWithoutWorkLogs()
    {
        var requirement = new Requirement { Title = "无工时登记" };

        requirement.LoggedHours.Should().Be(0m);
    }

    [Fact]
    public void EstimatedAndRemainingHours_AreIndependent()
    {
        var requirement = new Requirement
        {
            Title = "工时三字段",
            EstimatedHours = 8m,
            RemainingHours = 5m,
        };

        requirement.EstimatedHours.Should().Be(8m);
        requirement.RemainingHours.Should().Be(5m);
    }
}

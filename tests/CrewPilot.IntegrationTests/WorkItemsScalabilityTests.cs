using System.Diagnostics;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.WorkItems;
using CrewPilot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrewPilot.IntegrationTests;

/// <summary>500+ 工作项规模验证：列表查询应稳定返回全量且不出现明显超时。</summary>
public sealed class WorkItemsScalabilityTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task WorkItemList_HandlesFiveHundredItems()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrewPilotDbContext>();
            var seedItems = Enumerable.Range(1, 500).Select(index => new Requirement
            {
                ProjectId = project.Id,
                Title = $"规模测试需求 {index}",
                Status = RequirementStatus.Backlog,
            });
            db.WorkItems.AddRange(seedItems);
            await db.SaveChangesAsync();
        }

        var stopwatch = Stopwatch.StartNew();
        var items = await client.GetFromJsonAsync<List<WorkItemListItemDto>>(
            $"/api/projects/{project.Id}/workitems?type=requirement");
        stopwatch.Stop();

        items.Should().HaveCount(500);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }
}

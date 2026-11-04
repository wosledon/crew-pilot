using CrewPilot.Api.Contracts;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class ReportsController(CrewPilotDbContext db) : ControllerBase
{
    /// <summary>项目工时报表：总计 + 按类型/指派人/登记成员分组（预估 vs 已耗）。</summary>
    [HttpGet("api/projects/{projectId:guid}/hours-report")]
    public async Task<ActionResult<HoursReportDto>> HoursReport(Guid projectId, CancellationToken ct)
    {
        var items = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .Select(w => new
            {
                w.Id,
                Type = EF.Property<string>(w, "WorkItemType"),
                w.EstimatedHours,
                AssigneeName = w.Assignee != null ? w.Assignee.DisplayName : null,
            })
            .ToListAsync(ct);

        var ids = items.Select(i => i.Id).ToList();
        var logs = ids.Count == 0
            ? []
            : await db.WorkLogs
                .AsNoTracking()
                .Where(l => ids.Contains(l.WorkItemId))
                .Select(l => new
                {
                    l.WorkItemId,
                    l.Hours,
                    l.Date,
                    MemberName = l.Member != null ? l.Member.DisplayName : null,
                })
                .ToListAsync(ct);

        // SQLite 不支持 DateTimeOffset 排序，这里按 DateOnly 排序（DateOnly 可下推）
        var logged = logs
            .GroupBy(l => l.WorkItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Hours));
        decimal LoggedOf(Guid id) => logged.TryGetValue(id, out var hours) ? hours : 0m;

        var byType = items
            .GroupBy(i => i.Type)
            .Select(g => new HoursByGroupDto(
                g.Key,
                g.Key,
                g.Sum(i => i.EstimatedHours) ?? 0m,
                g.Sum(i => LoggedOf(i.Id))))
            .OrderBy(g => g.Key)
            .ToList();

        var byAssignee = items
            .GroupBy(i => i.AssigneeName ?? "unassigned")
            .Select(g => new HoursByGroupDto(
                g.Key,
                g.Key == "unassigned" ? "未指派" : g.Key,
                g.Sum(i => i.EstimatedHours) ?? 0m,
                g.Sum(i => LoggedOf(i.Id))))
            .OrderByDescending(g => g.LoggedHours)
            .ToList();

        // 按登记成员：预估工时只计入该工作项的首个登记成员，避免多成员重复计预估
        var firstMemberByItem = logs
            .GroupBy(l => l.WorkItemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Date).First().MemberName ?? "system");
        var estimateByItem = items.ToDictionary(i => i.Id, i => i.EstimatedHours ?? 0m);
        var memberKeys = logs.Select(l => l.MemberName ?? "system").Distinct().ToList();
        var byMember = memberKeys
            .Select(member =>
            {
                var memberItems = firstMemberByItem
                    .Where(pair => pair.Value == member)
                    .Select(pair => pair.Key)
                    .ToHashSet();
                return new HoursByGroupDto(
                    member,
                    member == "system" ? "系统（未归属）" : member,
                    memberItems.Sum(id => estimateByItem.GetValueOrDefault(id)),
                    logs.Where(l => (l.MemberName ?? "system") == member).Sum(l => l.Hours));
            })
            .OrderByDescending(g => g.LoggedHours)
            .ToList();

        return new HoursReportDto(
            items.Sum(i => i.EstimatedHours) ?? 0m,
            items.Sum(i => LoggedOf(i.Id)),
            byType,
            byAssignee,
            byMember);
    }

    /// <summary>成本报表：AgentRun token × 模型单价（models.dev / 手动目录价格）。</summary>
    [HttpGet("api/projects/{projectId:guid}/cost-report")]
    public async Task<ActionResult<CostReportDto>> CostReport(
        Guid projectId, CancellationToken ct)
    {
        var runs = await db.AgentRuns
            .AsNoTracking()
            .Where(r => r.WorkItem != null && r.WorkItem.ProjectId == projectId)
            .Select(r => new
            {
                r.PromptTokens,
                r.CompletionTokens,
                r.Agent.ProviderId,
                r.Agent.Model,
                ProviderName = r.Agent.Provider.Name,
            })
            .ToListAsync(ct);

        if (runs.Count == 0)
        {
            return new CostReportDto(0, 0, 0m, []);
        }

        var providerIds = runs.Select(r => r.ProviderId).Distinct().ToList();
        var catalog = await db.ModelCatalogEntries
            .AsNoTracking()
            .Where(m => providerIds.Contains(m.ProviderId))
            .Select(m => new
            {
                m.ProviderId,
                m.ModelId,
                m.InputPricePerMillion,
                m.OutputPricePerMillion,
            })
            .ToListAsync(ct);

        var prices = catalog
            .GroupBy(m => (m.ProviderId, Model: m.ModelId.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.First());

        var byModel = runs
            .GroupBy(r => (r.ProviderId, r.ProviderName, r.Model))
            .Select(group =>
            {
                var prompt = group.Sum(r => r.PromptTokens ?? 0);
                var completion = group.Sum(r => r.CompletionTokens ?? 0);
                prices.TryGetValue((group.Key.ProviderId, group.Key.Model.ToLowerInvariant()), out var price);
                var cost =
                    (decimal)prompt / 1_000_000m * (price?.InputPricePerMillion ?? 0m) +
                    (decimal)completion / 1_000_000m * (price?.OutputPricePerMillion ?? 0m);
                return new CostByModelDto(
                    group.Key.ProviderName,
                    group.Key.Model,
                    prompt,
                    completion,
                    Math.Round(cost, 6));
            })
            .OrderByDescending(m => m.Cost)
            .ToList();

        return new CostReportDto(
            runs.Sum(r => r.PromptTokens ?? 0),
            runs.Sum(r => r.CompletionTokens ?? 0),
            Math.Round(byModel.Sum(m => m.Cost), 6),
            byModel);
    }
}

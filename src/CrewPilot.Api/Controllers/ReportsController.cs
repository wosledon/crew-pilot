using CrewPilot.Api.Contracts;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class ReportsController(CrewPilotDbContext db) : ControllerBase
{
    /// <summary>项目工时报表：总计 + 按类型/指派人/登记成员分组（预估 vs 已耗），可按迭代/日期过滤。</summary>
    [HttpGet("api/projects/{projectId:guid}/hours-report")]
    public async Task<ActionResult<HoursReportDto>> HoursReport(
        Guid projectId,
        [FromQuery] Guid? iterationId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var itemQuery = db.WorkItems
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId);
        if (iterationId is Guid scopeIterationId)
        {
            itemQuery = itemQuery.Where(w => w.IterationId == scopeIterationId);
        }

        var items = await itemQuery
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

        // 日期范围在内存过滤（DateOnly 比较；SQLite provider 下推支持有限）
        logs = logs
            .Where(l => (from is null || l.Date >= from.Value) &&
                        (to is null || l.Date <= to.Value))
            .ToList();

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

    /// <summary>人力成本报表：工时 × 成员时薪（未设置时薪按 0 计）。</summary>
    [HttpGet("api/projects/{projectId:guid}/human-cost-report")]
    public async Task<ActionResult<HumanCostReportDto>> HumanCostReport(
        Guid projectId, CancellationToken ct)
    {
        var itemIds = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .Select(w => w.Id)
            .ToListAsync(ct);

        var logs = itemIds.Count == 0
            ? []
            : await db.WorkLogs
                .AsNoTracking()
                .Where(l => itemIds.Contains(l.WorkItemId))
                .Select(l => new
                {
                    l.Hours,
                    MemberName = l.Member != null ? l.Member.DisplayName : null,
                    MemberRate = l.Member != null ? l.Member.HourlyRate : null,
                })
                .ToListAsync(ct);

        var byMember = logs
            .GroupBy(l => l.MemberName ?? "system")
            .Select(group =>
            {
                var hours = group.Sum(l => l.Hours);
                var rate = group.First().MemberRate;
                return new HumanCostByMemberDto(
                    group.Key,
                    group.Key == "system" ? "系统（未归属）" : group.Key,
                    hours,
                    rate,
                    Math.Round(hours * (rate ?? 0m), 2));
            })
            .OrderByDescending(m => m.Cost)
            .ToList();

        return new HumanCostReportDto(
            logs.Sum(l => l.Hours),
            Math.Round(byMember.Sum(m => m.Cost), 2),
            byMember);
    }

    /// <summary>估算偏差报表：按工作项类型对比预估与已耗工时，统计超支项。</summary>
    [HttpGet("api/projects/{projectId:guid}/estimate-deviation")]
    public async Task<ActionResult<EstimateDeviationReportDto>> EstimateDeviation(
        Guid projectId, CancellationToken ct)
    {
        var items = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .Select(w => new
            {
                w.Id,
                Type = EF.Property<string>(w, "WorkItemType"),
                w.EstimatedHours,
            })
            .ToListAsync(ct);

        var ids = items.Select(i => i.Id).ToList();
        var logs = ids.Count == 0
            ? []
            : await db.WorkLogs
                .AsNoTracking()
                .Where(l => ids.Contains(l.WorkItemId))
                .Select(l => new { l.WorkItemId, l.Hours })
                .ToListAsync(ct);
        var loggedByItem = logs
            .GroupBy(l => l.WorkItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Hours));
        var loggedOf = (Guid id) => loggedByItem.GetValueOrDefault(id);

        var rows = items
            .GroupBy(i => i.Type)
            .Select(group =>
            {
                var estimated = group.Sum(i => i.EstimatedHours ?? 0m);
                var logged = group.Sum(i => loggedOf(i.Id));
                var deviation = logged - estimated;
                var overruns = group.Count(i => loggedOf(i.Id) > (i.EstimatedHours ?? 0m));
                return new EstimateDeviationRowDto(
                    group.Key,
                    group.Key,
                    estimated,
                    logged,
                    deviation,
                    estimated > 0 ? Math.Round(deviation / estimated * 100m, 1) : null,
                    group.Count(),
                    overruns);
            })
            .OrderBy(r => r.Key)
            .ToList();

        var totalEstimated = rows.Sum(r => r.EstimatedHours);
        var totalLogged = rows.Sum(r => r.LoggedHours);
        return new EstimateDeviationReportDto(
            totalEstimated,
            totalLogged,
            totalLogged - totalEstimated,
            totalEstimated > 0
                ? Math.Round((totalLogged - totalEstimated) / totalEstimated * 100m, 1)
                : null,
            items.Count,
            rows.Sum(r => r.OverrunCount),
            rows);
    }

    /// <summary>成本报表：AgentRun token × 模型单价，可按迭代/日期过滤。</summary>
    [HttpGet("api/projects/{projectId:guid}/cost-report")]
    public async Task<ActionResult<CostReportDto>> CostReport(
        Guid projectId,
        [FromQuery] Guid? iterationId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var runQuery = db.AgentRuns
            .AsNoTracking()
            .Where(r => r.WorkItem != null && r.WorkItem.ProjectId == projectId);
        if (iterationId is Guid scopeIterationId)
        {
            runQuery = runQuery.Where(r => r.WorkItem!.IterationId == scopeIterationId);
        }

        var runs = await runQuery
            .Select(r => new
            {
                r.PromptTokens,
                r.CompletionTokens,
                r.CreatedAt,
                r.Agent.ProviderId,
                r.Agent.Model,
                ProviderName = r.Agent.Provider.Name,
            })
            .ToListAsync(ct);

        // 日期范围在内存过滤（DateTimeOffset 在 SQLite 下排序/比较不可下推）
        runs = runs
            .Where(r =>
            {
                var date = DateOnly.FromDateTime(r.CreatedAt.LocalDateTime);
                return (from is null || date >= from.Value) &&
                       (to is null || date <= to.Value);
            })
            .ToList();

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

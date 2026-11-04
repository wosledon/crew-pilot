using CrewPilot.Api.Contracts;
using CrewPilot.Core.WorkItems;
using CrewPilot.Infrastructure.Audit;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>工作项依赖：甘特图排程与关键路径的基础数据（FS/SS）。</summary>
[ApiController]
public sealed class WorkItemDependenciesController(
    CrewPilotDbContext db,
    AuditLogger audit) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/dependencies")]
    public async Task<ActionResult<IReadOnlyList<WorkItemDependencyDto>>> List(
        Guid projectId, CancellationToken ct)
    {
        var rows = await db.WorkItemDependencies
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .Select(d => new
            {
                d.Id,
                d.ProjectId,
                d.FromWorkItemId,
                FromTitle = d.FromWorkItem.Title,
                d.ToWorkItemId,
                ToTitle = d.ToWorkItem.Title,
                d.Type,
                d.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(rows
            .OrderBy(d => d.CreatedAt)
            .Select(d => new WorkItemDependencyDto(
                d.Id, d.ProjectId, d.FromWorkItemId, d.FromTitle,
                d.ToWorkItemId, d.ToTitle,
                d.Type.ToString().ToLowerInvariant(), d.CreatedAt))
            .ToList());
    }

    /// <summary>关键路径：按工作项预估工时（缺失按 1h）加权的最长依赖链。</summary>
    [HttpGet("api/projects/{projectId:guid}/critical-path")]
    public async Task<ActionResult<CriticalPathDto>> CriticalPath(
        Guid projectId, CancellationToken ct)
    {
        var items = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .Select(w => new { w.Id, Hours = w.EstimatedHours ?? 1m })
            .ToListAsync(ct);
        var edges = await db.WorkItemDependencies
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .Select(d => new { d.FromWorkItemId, d.ToWorkItemId })
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            return Ok(new CriticalPathDto(0m, []));
        }

        var weights = items.ToDictionary(i => i.Id, i => i.Hours);
        var outgoing = items.ToDictionary(i => i.Id, _ => new List<Guid>());
        var indegree = items.ToDictionary(i => i.Id, _ => 0);
        foreach (var edge in edges)
        {
            if (!outgoing.ContainsKey(edge.FromWorkItemId) ||
                !weights.ContainsKey(edge.ToWorkItemId))
            {
                continue;
            }

            outgoing[edge.FromWorkItemId].Add(edge.ToWorkItemId);
            indegree[edge.ToWorkItemId]++;
        }

        // Kahn 拓扑排序 + DP：dp[node] = 自身工时 + max(前置 dp)
        var queue = new Queue<Guid>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var dp = new Dictionary<Guid, decimal>();
        var previous = new Dictionary<Guid, Guid>();
        foreach (var item in items)
        {
            dp[item.Id] = item.Hours;
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in outgoing[current])
            {
                var candidate = dp[current] + weights[next];
                if (candidate > dp[next])
                {
                    dp[next] = candidate;
                    previous[next] = current;
                }

                if (--indegree[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        var end = dp.OrderByDescending(pair => pair.Value).First().Key;
        var path = new List<Guid>();
        for (var node = end; ; node = previous[node])
        {
            path.Add(node);
            if (!previous.TryGetValue(node, out var prior))
            {
                break;
            }
        }

        path.Reverse();
        return Ok(new CriticalPathDto(dp[end], path));
    }

    [HttpPost("api/projects/{projectId:guid}/dependencies")]
    public async Task<ActionResult<WorkItemDependencyDto>> Create(
        Guid projectId,
        [FromBody] CreateWorkItemDependencyRequest request,
        CancellationToken ct)
    {
        if (request.FromWorkItemId == request.ToWorkItemId)
        {
            return Problem(detail: "工作项不能依赖自身", statusCode: 400);
        }

        var items = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId &&
                        (w.Id == request.FromWorkItemId || w.Id == request.ToWorkItemId))
            .Select(w => new { w.Id, w.Title })
            .ToListAsync(ct);
        var from = items.FirstOrDefault(w => w.Id == request.FromWorkItemId);
        var to = items.FirstOrDefault(w => w.Id == request.ToWorkItemId);
        if (from is null || to is null)
        {
            return Problem(detail: "工作项不存在或不属于该项目", statusCode: 400);
        }

        var duplicate = await db.WorkItemDependencies.AnyAsync(
            d => d.ProjectId == projectId &&
                 d.FromWorkItemId == request.FromWorkItemId &&
                 d.ToWorkItemId == request.ToWorkItemId,
            ct);
        if (duplicate)
        {
            return Problem(detail: "依赖已存在", statusCode: 409);
        }

        var edges = await db.WorkItemDependencies
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .Select(d => new { d.FromWorkItemId, d.ToWorkItemId })
            .ToListAsync(ct);
        if (CreatesCycle(
                edges.Select(e => (e.FromWorkItemId, e.ToWorkItemId)),
                request.FromWorkItemId,
                request.ToWorkItemId))
        {
            return Problem(detail: "依赖会形成环路", statusCode: 400);
        }

        var type = ParseType(request.Type);
        var dependency = new WorkItemDependency
        {
            ProjectId = projectId,
            FromWorkItemId = request.FromWorkItemId,
            ToWorkItemId = request.ToWorkItemId,
            Type = type,
        };
        db.WorkItemDependencies.Add(dependency);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(
            "dependency.create", "workitem-dependency", dependency.Id.ToString(),
            projectId, $"{from.Title} -> {to.Title} ({type})", ct);

        return Created($"/api/dependencies/{dependency.Id}", new WorkItemDependencyDto(
            dependency.Id, projectId, from.Id, from.Title, to.Id, to.Title,
            type.ToString().ToLowerInvariant(), dependency.CreatedAt));
    }

    [HttpDelete("api/dependencies/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var dependency = await db.WorkItemDependencies.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dependency is null)
        {
            return Problem(detail: "依赖不存在", statusCode: 404);
        }

        db.WorkItemDependencies.Remove(dependency);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(
            "dependency.delete", "workitem-dependency", id.ToString(),
            dependency.ProjectId, ct: ct);
        return NoContent();
    }

    /// <summary>新增 From→To 后，若从 To 可达 From 即形成环路。</summary>
    private static bool CreatesCycle(
        IEnumerable<(Guid From, Guid To)> edges,
        Guid from,
        Guid to)
    {
        var adjacency = edges
            .GroupBy(e => e.From)
            .ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());
        if (!adjacency.TryGetValue(from, out var direct))
        {
            adjacency[from] = direct = [];
        }

        direct.Add(to);

        var stack = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        stack.Push(to);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == from)
            {
                return true;
            }

            if (!visited.Add(current) || !adjacency.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var target in next)
            {
                stack.Push(target);
            }
        }

        return false;
    }

    private static DependencyType ParseType(string? value)
    {
        var normalized = (value ?? string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Trim();
        return normalized.ToLowerInvariant() switch
        {
            "starttostart" or "ss" => DependencyType.StartToStart,
            _ => DependencyType.FinishToStart,
        };
    }
}

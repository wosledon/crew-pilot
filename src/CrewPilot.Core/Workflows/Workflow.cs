namespace CrewPilot.Core.Workflows;

using CrewPilot.Core.Agents;
using CrewPilot.Core.Common;
using CrewPilot.Core.Projects;

/// <summary>步骤通过方式（PRD 5.4，全局默认仅卡点时询问）。</summary>
public enum ApprovalPolicy
{
    /// <summary>自动通过：Agent 完成后直接进入下一步。</summary>
    Auto,

    /// <summary>仅卡点时询问：Agent 自行判断，遇阻塞才挂起。</summary>
    BlockersOnly,

    /// <summary>总是询问：每步完成都等待用户确认。</summary>
    Always,
}

/// <summary>工作流：有向步骤编排，可项目内定义，也可作为全局模板（ProjectId 为空）。</summary>
public class Workflow : Entity
{
    /// <summary>所属项目；模板工作流为 null。</summary>
    public Guid? ProjectId { get; set; }

    public Projects.Project? Project { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>是否内置模板（模板可被项目实例化）。</summary>
    public bool IsTemplate { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
}

/// <summary>工作流步骤：执行 Agent（可指定具体 Agent 或按岗位解析）、指令与审批策略。</summary>
public class WorkflowStep : Entity
{
    public Guid WorkflowId { get; set; }

    public Workflow Workflow { get; set; } = null!;

    /// <summary>步骤顺序（从 0 开始）。</summary>
    public int Order { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>指定执行 Agent（为空时按 RequiredJob 解析）。</summary>
    public Guid? AgentId { get; set; }

    public Agent? Agent { get; set; }

    /// <summary>按岗位解析执行 Agent（模板工作流使用）。</summary>
    public AgentJob? RequiredJob { get; set; }

    /// <summary>步骤指令（注入到 Agent 提示词）。</summary>
    public string? Instruction { get; set; }

    public ApprovalPolicy ApprovalPolicy { get; set; } = ApprovalPolicy.BlockersOnly;

    /// <summary>失败自动重试次数。</summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>输入契约（说明文字，供提示词与审计使用）。</summary>
    public string? InputContract { get; set; }

    /// <summary>输出契约（说明文字）。</summary>
    public string? OutputContract { get; set; }

    /// <summary>并行步骤：与相邻同 Order 且 Parallel 的步骤并发执行。</summary>
    public bool Parallel { get; set; }
}

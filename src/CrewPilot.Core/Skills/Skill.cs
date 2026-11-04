namespace CrewPilot.Core.Skills;

using CrewPilot.Core.Agents;
using CrewPilot.Core.Common;

/// <summary>Skill：可复用能力包 = 提示词模板 +（可选）工具白名单 + 脚本/资源（PRD 5.5）。</summary>
public class Skill : Entity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>注入到 Agent 系统提示词的能力说明/模板。</summary>
    public string PromptContent { get; set; } = string.Empty;

    /// <summary>工具白名单（JSON 数组，可选；为空表示沿用 Agent 配置）。</summary>
    public string? Tools { get; set; }

    /// <summary>脚本/资源目录（可选，相对 Skill 根目录）。</summary>
    public string? ScriptPath { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AgentSkill> Agents { get; set; } = new List<AgentSkill>();
}

/// <summary>Agent ↔ Skill 装配关系。</summary>
public class AgentSkill : Entity
{
    public Guid AgentId { get; set; }

    public Agent Agent { get; set; } = null!;

    public Guid SkillId { get; set; }

    public Skill Skill { get; set; } = null!;
}

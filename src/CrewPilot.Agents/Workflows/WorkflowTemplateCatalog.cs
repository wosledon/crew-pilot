using CrewPilot.Core.Agents;
using CrewPilot.Core.Workflows;

namespace CrewPilot.Agents.Workflows;

public sealed record WorkflowTemplateStep(
    string Name,
    AgentJob Job,
    string? Instruction,
    ApprovalPolicy ApprovalPolicy = ApprovalPolicy.BlockersOnly,
    int MaxRetries = 1);

public sealed record WorkflowTemplate(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<WorkflowTemplateStep> Steps);

/// <summary>内置三条工作流模板（PRD 5.4：需求交付 / 缺陷修复 / 发布）。</summary>
public static class WorkflowTemplateCatalog
{
    public static IReadOnlyList<WorkflowTemplate> All { get; } =
    [
        new(
            "requirement-delivery",
            "需求交付流",
            "需求就绪 → 架构方案 → 开发实现 → 自动化测试 → 冒烟 → 提 PR → 合并",
            [
                new("需求分析", AgentJob.Product, "梳理需求背景、范围与验收标准，输出结构化需求。"),
                new("技术方案", AgentJob.Architect, "基于需求输出技术方案、影响范围与风险点。"),
                new("开发实现", AgentJob.Developer, "在隔离工作目录中实现需求并补充单元测试。"),
                new("自动化测试", AgentJob.Qa, "运行自动化测试，输出测试报告；发现问题请给出缺陷清单。"),
                new("冒烟测试", AgentJob.Qa, "执行冒烟用例，确认核心链路可用。"),
                new("提交 PR", AgentJob.Developer, "提交 PR 并给出 PR 链接与变更说明。"),
                new("合并", AgentJob.Developer, "确认 CI 通过后合并 PR。", ApprovalPolicy.Always),
            ]),
        new(
            "defect-fix",
            "缺陷修复流",
            "缺陷确认 → 修复 → 验证 → 关闭",
            [
                new("缺陷确认", AgentJob.Qa, "复现缺陷并确认严重级别与影响范围。"),
                new("缺陷修复", AgentJob.Developer, "定位根因并修复，补充回归测试。"),
                new("验证", AgentJob.Qa, "验证修复结果并执行回归。"),
                new("关闭", AgentJob.Qa, "确认通过后关闭缺陷。", ApprovalPolicy.Always),
            ]),
        new(
            "release",
            "发布流",
            "生成 changelog → 发布审批 → 打 tag / 创建 Release",
            [
                new("生成 changelog", AgentJob.Ops, "汇总本次发布内容，生成 changelog。"),
                new("发布审批", AgentJob.Ops, "发布到生产环境，需要人工确认。", ApprovalPolicy.Always),
                new("创建 Release", AgentJob.Ops, "打 tag 并创建 Release。"),
            ]),
    ];

    public static WorkflowTemplate? Find(string key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}

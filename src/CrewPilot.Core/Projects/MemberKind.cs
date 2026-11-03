namespace CrewPilot.Core.Projects;

/// <summary>成员类型：人类用户或 Agent 数字员工。</summary>
public enum MemberKind
{
    User,
    Agent,
}

/// <summary>项目角色，与产品中的岗位一一对应。</summary>
public enum ProjectRole
{
    Owner,
    Product,
    Architect,
    Developer,
    Qa,
    Manager,
    Ops,
    Admin,
}

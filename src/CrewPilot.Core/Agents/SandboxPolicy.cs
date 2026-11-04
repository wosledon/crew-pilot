namespace CrewPilot.Core.Agents;

/// <summary>Agent 沙箱策略：决定文件写入与终端命令能力。</summary>
public enum SandboxPolicy
{
    /// <summary>默认：文件读写限制在项目工作目录，终端命令必须命中白名单。</summary>
    WorkspaceWrite,

    /// <summary>只读：仅允许读取/列目录，禁止写入与执行命令。</summary>
    ReadOnly,

    /// <summary>受信：文件仍限制在工作目录，但终端命令不需白名单（仍不经 shell、有超时）。</summary>
    Trusted,
}

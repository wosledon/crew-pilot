namespace CrewPilot.Agents.Execution;

/// <summary>工时折算：AgentRun 耗时 1:1 折算为工时（不加权，保留 2 位小数）。</summary>
public static class AgentRunHours
{
    public static decimal FromDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 0m;
        }

        return Math.Round((decimal)duration.TotalHours, 2);
    }
}

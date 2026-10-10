namespace Runner.Business.Diagnostics;

/// <summary>一条诊断事件：墙钟时间、来源标签、一行文字。结构化数值都写在 Message 里，导出后人读机读都够。</summary>
public sealed record LogEntry(DateTimeOffset Time, string Source, string Message)
{
    public string ToLine() => $"{Time:HH:mm:ss.fff} [{Source}] {Message}";
}

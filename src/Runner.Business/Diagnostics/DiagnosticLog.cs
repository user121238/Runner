using System.Text;
using System.Text.Json;

namespace Runner.Business.Diagnostics;

/// <summary>
/// 进程内诊断事件缓冲：原生来源、会话、宿主在事件发生处直接写入，诊断页展示并导出。
/// 有界（满 <see cref="Capacity"/> 条时整批丢最旧的），任意线程可写；Changed 在锁外触发，订阅方自行切主线程。
/// </summary>
public static class DiagnosticLog
{
    public const int Capacity = 10_000;
    private const int TrimBatch = 1_000;
    private static readonly object _sync = new();
    private static readonly List<LogEntry> _entries = new();

    public static event Action? Changed;

    public static int Count { get { lock (_sync) return _entries.Count; } }

    public static void Write(string source, string message)
    {
        lock (_sync)
        {
            if (_entries.Count >= Capacity) _entries.RemoveRange(0, TrimBatch);
            _entries.Add(new LogEntry(DateTimeOffset.Now, source, message));
        }
        Changed?.Invoke();
    }

    /// <summary>最近 count 条，新的在前，供界面显示。</summary>
    public static LogEntry[] Latest(int count)
    {
        lock (_sync)
        {
            int take = Math.Min(count, _entries.Count);
            var result = new LogEntry[take];
            for (int i = 0; i < take; i++) result[i] = _entries[_entries.Count - 1 - i];
            return result;
        }
    }

    /// <summary>全部条目按时间正序，供导出。</summary>
    public static LogEntry[] Snapshot()
    {
        lock (_sync) return _entries.ToArray();
    }

    public static void Clear()
    {
        lock (_sync) _entries.Clear();
        Changed?.Invoke();
    }

    public static string ToText()
    {
        var entries = Snapshot();
        var builder = new StringBuilder(entries.Length * 96);
        foreach (var entry in entries) builder.AppendLine(entry.ToLine());
        return builder.ToString();
    }

    public static string ToJson() => JsonSerializer.Serialize(Snapshot(), LogEntryJsonContext.Default.LogEntryArray);
}

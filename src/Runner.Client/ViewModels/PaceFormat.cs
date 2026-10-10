namespace Runner.Client.ViewModels;

/// <summary>配速文本：5'32"；无值为 —。</summary>
public static class PaceFormat
{
    public static string Text(TimeSpan? pace) =>
        pace is { } p ? $"{(int)p.TotalMinutes}'{p.Seconds:00}\"" : "—";
}

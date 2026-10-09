using Runner.Core.Activities;

namespace Runner.Client.ViewModels;

/// <summary>记录列表项：开始时间与一行摘要。</summary>
public sealed class ActivityListItem
{
    public ActivityListItem(ActivityRecord record)
    {
        Record = record;
        Title = record.StartTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        Summary = Describe(record);
    }

    public ActivityRecord Record { get; }
    public int Id => Record.Id;
    public string Title { get; }
    public string Summary { get; }

    public static string Describe(ActivityRecord record)
    {
        var summary = $"{record.DistanceMeters / 1000:F2} 公里 · {FormatDuration(record.ElapsedSeconds)} · 均步频 {record.AverageCadence:F0}";
        return string.IsNullOrEmpty(record.WorkoutName) ? summary : $"{summary} · {record.WorkoutName}";
    }

    public static string FormatDuration(double seconds) =>
        TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
}

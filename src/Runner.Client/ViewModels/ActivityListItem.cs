using Runner.Core.Activities;
using Runner.Core.Session;

namespace Runner.Client.ViewModels;

/// <summary>记录列表项：距离为主数字，日期、时长 / 配速 / 均步频一行，课程名作标签。</summary>
public sealed class ActivityListItem
{
    public ActivityListItem(ActivityRecord record)
    {
        Record = record;
        var start = record.StartTimeUtc.ToLocalTime();
        Title = start.ToString("yyyy-MM-dd HH:mm");
        Date = start.ToString("M月d日 HH:mm");
        Distance = (record.DistanceMeters / 1000).ToString("F2");
        Detail = $"{FormatDuration(record.ElapsedSeconds)} · 配速 {PaceFormat.Text(PaceCalculator.Average(TimeSpan.FromSeconds(record.ElapsedSeconds), record.DistanceMeters))} · 均步频 {record.AverageCadence:F0}";
        WorkoutName = record.WorkoutName ?? "";
        Summary = string.IsNullOrEmpty(WorkoutName) ? Detail : $"{Detail} · {WorkoutName}";
    }

    public ActivityRecord Record { get; }
    public int Id => Record.Id;
    public string Title { get; }
    public string Date { get; }
    public string Distance { get; }
    public string Detail { get; }
    public string WorkoutName { get; }
    public bool HasWorkout => WorkoutName.Length > 0;
    public string Summary { get; }

    public static string FormatDuration(double seconds) =>
        TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
}

using Runner.Core.Session;

namespace Runner.Core.Activities;

/// <summary>一次跑步的记录：由已结束的会话生成，Id 由存储分配。</summary>
public class ActivityRecord
{
    /// <summary>短于此时长的会话不落盘，避免误触产生垃圾记录。</summary>
    public static readonly TimeSpan MinimumElapsed = TimeSpan.FromSeconds(5);

    public int Id { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public double ElapsedSeconds { get; set; }
    public double DistanceMeters { get; set; }
    public int StepCount { get; set; }
    public double AverageCadence { get; set; }
    public double AverageSpeedKmh { get; set; }
    public string? WorkoutName { get; set; }
    public List<TrackPoint> TrackPoints { get; set; } = new();
    public List<CadencePoint> CadencePoints { get; set; } = new();
    /// <summary>（跑动秒数, 累计米数）序列：详情页据此重算最快 / 最慢配速；旧记录没有，留空。</summary>
    public List<DistancePoint> DistanceSamples { get; set; } = new();

    public static bool ShouldRecord(RunSession session) =>
        session.State == RunSessionState.Stopped && session.Elapsed >= MinimumElapsed;

    public static ActivityRecord FromSession(RunSession session, string? workoutName)
    {
        if (session.State != RunSessionState.Stopped)
            throw new InvalidOperationException("只能从已结束的会话生成记录");

        return new ActivityRecord
        {
            StartTimeUtc = session.StartTime!.Value.UtcDateTime,
            EndTimeUtc = session.EndTime!.Value.UtcDateTime,
            ElapsedSeconds = session.Elapsed.TotalSeconds,
            DistanceMeters = session.DistanceMeters,
            StepCount = session.StepCount,
            AverageCadence = session.AverageCadence,
            AverageSpeedKmh = session.AverageSpeedKmh,
            WorkoutName = workoutName,
            TrackPoints = session.TrackPoints.Select(TrackPoint.From).ToList(),
            CadencePoints = session.CadenceSamples.Select(CadencePoint.From).ToList(),
            DistanceSamples = session.DistanceSamples.Select(DistancePoint.From).ToList()
        };
    }
}

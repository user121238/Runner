namespace Runner.Business.Models.WatchBle;

public class WatchSportSummary
{
    public long TotalDurationSeconds { get; set; }
    public int TotalDistanceMeters { get; set; }
    public int TotalCalories { get; set; }
    public int TotalSteps { get; set; }
    public int AvgHeartRate { get; set; }
    public int MaxHeartRate { get; set; }
    public double AvgCadence { get; set; }
    public double AvgPace { get; set; }
    public double AvgSpeedKmh { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    public TimeSpan Duration => TimeSpan.FromSeconds(TotalDurationSeconds);
    public double DistanceKm => TotalDistanceMeters / 1000.0;
    public string PaceText => AvgPace > 0
        ? $"{(int)(AvgPace / 60)}:{AvgPace % 60:F0}/km"
        : "--:--/km";
}

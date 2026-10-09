namespace Runner.Core.Geolocation;

public class LocationFilterConfig
{
    public double MinDistanceMeters { get; set; } = 2;
    public double MaxSpeedKmPerHour { get; set; } = 45;
    public double MinTimeIntervalSeconds { get; set; } = 0.3;
    public double MaxAccuracyMeters { get; set; } = 20;
    public double MaxLocationAgeSeconds { get; set; } = 10;
    public double MaxGapSeconds { get; set; } = 15;
    public double SpeedTimeoutSeconds { get; set; } = 3;

    internal LocationFilterConfig Snapshot()
    {
        if (!double.IsFinite(MinDistanceMeters) || MinDistanceMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(MinDistanceMeters));
        if (!double.IsFinite(MinTimeIntervalSeconds) || MinTimeIntervalSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(MinTimeIntervalSeconds));
        ValidatePositive(MaxSpeedKmPerHour, nameof(MaxSpeedKmPerHour));
        ValidatePositive(MaxAccuracyMeters, nameof(MaxAccuracyMeters));
        ValidatePositive(MaxLocationAgeSeconds, nameof(MaxLocationAgeSeconds));
        ValidatePositive(MaxGapSeconds, nameof(MaxGapSeconds));
        ValidatePositive(SpeedTimeoutSeconds, nameof(SpeedTimeoutSeconds));
        return (LocationFilterConfig)MemberwiseClone();
    }

    private static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }
}

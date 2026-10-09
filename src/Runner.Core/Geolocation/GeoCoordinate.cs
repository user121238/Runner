namespace Runner.Core.Geolocation
{
    /// <summary>
    /// GPS 坐标
    /// </summary>
    public readonly struct GeoCoordinate
    {
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double Altitude { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public double? HorizontalAccuracyMeters { get; init; }
        public double? SpeedMetersPerSecond { get; init; }

        public bool IsValid => double.IsFinite(Latitude) && Latitude >= -90 && Latitude <= 90 &&
            double.IsFinite(Longitude) && Longitude >= -180 && Longitude <= 180 &&
            double.IsFinite(Altitude) &&
            (HorizontalAccuracyMeters is not double accuracy || (double.IsFinite(accuracy) && accuracy >= 0));

        public GeoCoordinate(double latitude, double longitude, double altitude = 0, DateTimeOffset? timestamp = null,
            double? horizontalAccuracyMeters = null, double? speedMetersPerSecond = null)
        {
            Latitude = latitude;
            Longitude = longitude;
            Altitude = altitude;
            Timestamp = timestamp ?? DateTimeOffset.UtcNow;
            HorizontalAccuracyMeters = horizontalAccuracyMeters;
            SpeedMetersPerSecond = speedMetersPerSecond;
        }
    }
}

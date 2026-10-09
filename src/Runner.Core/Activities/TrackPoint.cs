using Runner.Core.Geolocation;

namespace Runner.Core.Activities;

/// <summary>轨迹点的持久化形态：时间存 UTC。</summary>
public class TrackPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public DateTime TimestampUtc { get; set; }
    public double? HorizontalAccuracyMeters { get; set; }

    public static TrackPoint From(GeoCoordinate coordinate) => new()
    {
        Latitude = coordinate.Latitude,
        Longitude = coordinate.Longitude,
        Altitude = coordinate.Altitude,
        TimestampUtc = coordinate.Timestamp.UtcDateTime,
        HorizontalAccuracyMeters = coordinate.HorizontalAccuracyMeters
    };
}

using Runner.Core.Geolocation;

namespace Runner.Core.Tests.Geolocation;

internal static class GeoTestData
{
    // Equatorial arc length for a spherical earth with radius 6,371,000 m.
    // This test fixture does not invoke the production distance calculation.
    private const double MetersPerLongitudeDegree = 111194.92664455874;

    public static GeoCoordinate Point(double meters, double seconds,
        double? accuracy = null, double? speed = null) =>
        new(0, meters / MetersPerLongitudeDegree, timestamp: ManualTimeProvider.Epoch.AddSeconds(seconds),
            horizontalAccuracyMeters: accuracy, speedMetersPerSecond: speed);

    public static bool Add(DistanceTracker tracker, ManualTimeProvider clock,
        double meters, double seconds, double? accuracy = null, double? speed = null)
    {
        clock.SetSeconds(seconds);
        return tracker.AddLocation(Point(meters, seconds, accuracy, speed));
    }
}

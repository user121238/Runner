using Runner.Core.Geolocation;

namespace Runner.Core.Tests.Geolocation;

[TestClass]
public sealed class GeoCalculatorTests
{
    [TestMethod]
    public void CalculateDistance_InvalidEndpoint_ThrowsWithEndpointName()
    {
        var valid = GeoTestData.Point(0, 0);
        var invalid = new GeoCoordinate(double.NaN, 0, timestamp: ManualTimeProvider.Epoch);

        var fromException = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => GeoCalculator.CalculateDistance(invalid, valid));
        var toException = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => GeoCalculator.CalculateDistance(valid, invalid));

        Assert.AreEqual("from", fromException.ParamName);
        Assert.AreEqual("to", toException.ParamName);
    }

    [TestMethod]
    [DataRow(0.0, 0.0, 0.0, 0.0, 0.0)]
    [DataRow(0.0, 0.0, 0.0, 1.0, 111194.926644559)]
    [DataRow(0.0, 0.0, 1.0, 0.0, 111194.926644559)]
    [DataRow(0.0, 179.999, 0.0, -179.999, 222.389853289)]
    [DataRow(90.0, 0.0, -90.0, 0.0, 20015086.7960206)]
    public void CalculateDistance_KnownSphericalArcs_ReturnsMeters(
        double fromLat, double fromLon, double toLat, double toLon, double expected)
    {
        var from = new GeoCoordinate(fromLat, fromLon, timestamp: ManualTimeProvider.Epoch);
        var to = new GeoCoordinate(toLat, toLon, timestamp: ManualTimeProvider.Epoch);

        Assert.AreEqual(expected, GeoCalculator.CalculateDistance(from, to), 0.0001);
        Assert.AreEqual(expected, GeoCalculator.CalculateDistance(to, from), 0.0001);
    }

    [TestMethod]
    public void CalculateDistance_AltitudeNoise_DoesNotAddVerticalDistance()
    {
        var from = new GeoCoordinate(30, 120, 0, ManualTimeProvider.Epoch);
        var to = new GeoCoordinate(30, 120, 50, ManualTimeProvider.Epoch.AddSeconds(1));

        Assert.AreEqual(0, GeoCalculator.CalculateDistance(from, to));
    }

    [TestMethod]
    public void CalculateSpeed_TenMetersInTwoSeconds_ReturnsMetersPerSecond()
    {
        double result = GeoCalculator.CalculateSpeed(GeoTestData.Point(0, 0), GeoTestData.Point(10, 2));

        Assert.AreEqual(5, result, 1e-9);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void CalculateSpeed_NonPositiveElapsedTime_ReturnsZero(int seconds)
    {
        double result = GeoCalculator.CalculateSpeed(GeoTestData.Point(0, 0), GeoTestData.Point(10, seconds));

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    [DataRow(0.0, 0.0)]
    [DataRow(3.0, 10.8)]
    [DataRow(12.5, 45.0)]
    public void SpeedConversions_KnownUnits_AreConsistent(double metersPerSecond, double kilometersPerHour)
    {
        Assert.AreEqual(kilometersPerHour, GeoCalculator.MetersPerSecondToKmPerHour(metersPerSecond), 1e-10);
        Assert.AreEqual(metersPerSecond, GeoCalculator.KmPerHourToMetersPerSecond(kilometersPerHour), 1e-10);
    }
}

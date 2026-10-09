using Runner.Core.Geolocation;

namespace Runner.Core.Tests.Geolocation;

[TestClass]
public sealed class LocationFilterTests
{
    [TestMethod]
    public void Filter_InvalidAltitude_IsRejectedBeforeAnchoring()
    {
        var filter = new LocationFilter();
        var point = new GeoCoordinate(0, 0, double.NaN, ManualTimeProvider.Epoch);

        Assert.AreEqual(FilterResult.RejectInvalidCoordinate, filter.Filter(point));
        Assert.IsNull(filter.LastAcceptedLocation);
    }

    [TestMethod]
    public void Filter_ZeroMinimumTimeInterval_StillRejectsDuplicateObservationTime()
    {
        var filter = new LocationFilter(new() { MinTimeIntervalSeconds = 0 });
        filter.Filter(GeoTestData.Point(0, 0));

        Assert.AreEqual(FilterResult.RejectTimeIntervalTooShort, filter.Filter(GeoTestData.Point(3, 0)));
        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(3, 1)));
    }

    [TestMethod]
    public void Filter_TooFrequentAfterSubthresholdObservation_DoesNotBypassObservationInterval()
    {
        var filter = new LocationFilter();
        filter.Filter(GeoTestData.Point(0, 0));
        filter.Filter(GeoTestData.Point(1, 1));

        Assert.AreEqual(FilterResult.RejectTimeIntervalTooShort, filter.Filter(GeoTestData.Point(3, 1.1)));
        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(3, 1.3)));
    }

    [TestMethod]
    public void Filter_ValidFirstPoint_IsAccepted()
    {
        var filter = new LocationFilter();

        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(0, 0, accuracy: 5)));
        Assert.IsNotNull(filter.LastAcceptedLocation);
        Assert.AreEqual(ManualTimeProvider.Epoch, filter.LastAcceptedLocation.Value.Timestamp);
    }

    [TestMethod]
    [DataRow(double.NaN, 0.0)]
    [DataRow(0.0, double.NaN)]
    [DataRow(double.PositiveInfinity, 0.0)]
    [DataRow(0.0, double.NegativeInfinity)]
    [DataRow(90.00001, 0.0)]
    [DataRow(-90.00001, 0.0)]
    [DataRow(0.0, 180.00001)]
    [DataRow(0.0, -180.00001)]
    public void Filter_InvalidFirstCoordinate_IsRejectedWithoutPoisoningNextPoint(double latitude, double longitude)
    {
        var filter = new LocationFilter();
        var invalid = new GeoCoordinate(latitude, longitude, timestamp: ManualTimeProvider.Epoch);

        Assert.AreNotEqual(FilterResult.Accept, filter.Filter(invalid));
        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(0, 1)));
    }

    [TestMethod]
    [DataRow(90.0, 180.0)]
    [DataRow(-90.0, -180.0)]
    public void Filter_CoordinateAtLegalBoundary_IsAccepted(double latitude, double longitude)
    {
        var filter = new LocationFilter();

        Assert.AreEqual(FilterResult.Accept,
            filter.Filter(new GeoCoordinate(latitude, longitude, timestamp: ManualTimeProvider.Epoch)));
    }

    [TestMethod]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(20.01)]
    public void Filter_InvalidOrPoorAccuracy_IsRejected(double accuracy)
    {
        var filter = new LocationFilter();

        Assert.AreNotEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(0, 0, accuracy)));
        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(0, 1, accuracy: 20)));
    }

    [TestMethod]
    public void Filter_OverlappingAccuracyCircles_DoesNotCountNoiseAsMovement()
    {
        var filter = new LocationFilter();
        filter.Filter(GeoTestData.Point(-4, 0, accuracy: 5));

        Assert.AreEqual(FilterResult.RejectDistanceTooSmall, filter.Filter(GeoTestData.Point(4, 2, accuracy: 5)));
        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(7, 4, accuracy: 5)));
    }

    [TestMethod]
    public void Filter_NoAccuracyProvided_PreservesMinimumDistanceBehavior()
    {
        var filter = new LocationFilter();
        filter.Filter(GeoTestData.Point(0, 0));

        Assert.AreEqual(FilterResult.RejectDistanceTooSmall, filter.Filter(GeoTestData.Point(1.99, 1)));
        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(2.01, 2)));
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(0.299)]
    public void Filter_DuplicateOutOfOrderOrTooFrequentPoint_IsRejected(double seconds)
    {
        var filter = new LocationFilter();
        filter.Filter(GeoTestData.Point(0, 0));

        Assert.AreNotEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(3, seconds)));
        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(3, 1)));
    }

    [TestMethod]
    public void Filter_TwentyFiveKilometersPerHour_IsValidRunningSpeed()
    {
        var filter = new LocationFilter();
        filter.Filter(GeoTestData.Point(0, 0));

        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(25 / 3.6, 1)));
    }

    [TestMethod]
    public void Filter_ImplausibleJump_DoesNotMoveDistanceAnchor()
    {
        var filter = new LocationFilter();
        filter.Filter(GeoTestData.Point(0, 0));

        Assert.AreEqual(FilterResult.RejectSpeedTooFast, filter.Filter(GeoTestData.Point(100, 1)));
        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(6, 2)));
    }

    [TestMethod]
    public void Reset_PreviousAnchor_NewPointStartsFresh()
    {
        var filter = new LocationFilter();
        filter.Filter(GeoTestData.Point(0, 100));

        filter.Reset();

        Assert.IsNull(filter.LastAcceptedLocation);
        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(10000, 0)));
    }
}

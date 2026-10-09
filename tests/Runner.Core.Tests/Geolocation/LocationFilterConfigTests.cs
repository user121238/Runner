using Runner.Core.Geolocation;

namespace Runner.Core.Tests.Geolocation;

[TestClass]
public sealed class LocationFilterConfigTests
{
    public static IEnumerable<(LocationFilterConfig config, string property)> InvalidConfigs =>
    [
        (new() { MinDistanceMeters = -1 }, nameof(LocationFilterConfig.MinDistanceMeters)),
        (new() { MinDistanceMeters = double.NaN }, nameof(LocationFilterConfig.MinDistanceMeters)),
        (new() { MinTimeIntervalSeconds = -1 }, nameof(LocationFilterConfig.MinTimeIntervalSeconds)),
        (new() { MinTimeIntervalSeconds = double.NaN }, nameof(LocationFilterConfig.MinTimeIntervalSeconds)),
        (new() { MaxSpeedKmPerHour = 0 }, nameof(LocationFilterConfig.MaxSpeedKmPerHour)),
        (new() { MaxAccuracyMeters = -1 }, nameof(LocationFilterConfig.MaxAccuracyMeters)),
        (new() { MaxLocationAgeSeconds = 0 }, nameof(LocationFilterConfig.MaxLocationAgeSeconds)),
        (new() { MaxGapSeconds = double.PositiveInfinity }, nameof(LocationFilterConfig.MaxGapSeconds)),
        (new() { SpeedTimeoutSeconds = double.NaN }, nameof(LocationFilterConfig.SpeedTimeoutSeconds))
    ];

    [TestMethod]
    [DynamicData(nameof(InvalidConfigs))]
    public void Constructor_InvalidConfiguration_RejectsNamedProperty(LocationFilterConfig config, string property)
    {
        var filterException = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LocationFilter(config));
        var trackerException = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DistanceTracker(config));

        Assert.AreEqual(property, filterException.ParamName);
        Assert.AreEqual(property, trackerException.ParamName);
    }

    [TestMethod]
    public void Filter_ConfigChangesAfterConstruction_DoNotAlterItsSnapshot()
    {
        var config = new LocationFilterConfig { MinDistanceMeters = 2 };
        var filter = new LocationFilter(config);
        config.MinDistanceMeters = 100;
        filter.Filter(GeoTestData.Point(0, 0));

        Assert.AreEqual(FilterResult.Accept, filter.Filter(GeoTestData.Point(3, 1)));
    }

    [TestMethod]
    public void DistanceTracker_ConfigChangesAfterConstruction_DoNotAlterItsSnapshot()
    {
        var config = new LocationFilterConfig { SpeedTimeoutSeconds = 3 };
        var clock = new ManualTimeProvider();
        var tracker = new DistanceTracker(config, clock);
        config.SpeedTimeoutSeconds = 100;
        GeoTestData.Add(tracker, clock, 0, 0);
        GeoTestData.Add(tracker, clock, 3, 1);
        clock.SetSeconds(4);

        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
    }
}

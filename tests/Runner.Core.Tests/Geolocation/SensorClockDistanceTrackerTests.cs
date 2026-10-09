using Runner.Core.Geolocation;
using Runner.Core.SignalProcessing;

namespace Runner.Core.Tests.Geolocation;

[TestClass]
public sealed class SensorClockDistanceTrackerTests
{
    private const long BootTime = 259_200_000_000_000;
    private long _nanoseconds = BootTime;

    [TestMethod]
    public void AddLocation_CurrentMonotonicSample_IsAccepted()
    {
        var clock = CreateClock();
        var tracker = new DistanceTracker(timeProvider: clock);
        _nanoseconds += 1_234_500_000;

        Assert.IsTrue(tracker.AddLocation(Point(clock, _nanoseconds), out var result));

        Assert.AreEqual(FilterResult.Accept, result);
        Assert.AreEqual(1, tracker.TrajectoryPointCount);
        Assert.AreEqual(ManualTimeProvider.Epoch.AddTicks(12_345_000), tracker.Trajectory[0].Timestamp);
    }

    [TestMethod]
    public void AddLocation_DelayedMonotonicSampleWithinAgeLimit_IsAccepted()
    {
        var clock = CreateClock();
        var tracker = new DistanceTracker(timeProvider: clock);
        long capturedAt = _nanoseconds + 1_000_000_000;
        _nanoseconds += 3_000_000_000;

        Assert.IsTrue(tracker.AddLocation(Point(clock, capturedAt), out var result));

        Assert.AreEqual(FilterResult.Accept, result);
        Assert.AreEqual(ManualTimeProvider.Epoch.AddSeconds(1), tracker.Trajectory[0].Timestamp);
    }

    [TestMethod]
    public void AddLocation_StaleMonotonicSample_IsRejectedWithoutAnchoring()
    {
        var clock = CreateClock();
        var tracker = new DistanceTracker(timeProvider: clock);
        long capturedAt = _nanoseconds;
        _nanoseconds += 10_000_000_100; // One DateTime tick beyond the age limit.

        Assert.IsFalse(tracker.AddLocation(Point(clock, capturedAt), out var result));

        Assert.AreEqual(FilterResult.RejectStaleLocation, result);
        Assert.AreEqual(0, tracker.TrajectoryPointCount);
        Assert.IsTrue(tracker.AddLocation(Point(clock, _nanoseconds)));
    }

    [TestMethod]
    public void AddLocation_ActuallyFutureMonotonicSample_IsStillRejected()
    {
        var clock = CreateClock();
        var tracker = new DistanceTracker(timeProvider: clock);

        Assert.IsFalse(tracker.AddLocation(Point(clock, _nanoseconds + 100), out var result));

        Assert.AreEqual(FilterResult.RejectFutureLocation, result);
        Assert.AreEqual(0, tracker.TrajectoryPointCount);
        Assert.IsTrue(tracker.AddLocation(Point(clock, _nanoseconds)));
    }

    [TestMethod]
    [DataRow(0.25)]
    [DataRow(90.0)]
    [DataRow(-90.0)]
    public void AddLocation_GpsUtcDiffersFromPhoneClock_MonotonicMappingAvoidsFalseRejection(double gpsSkewSeconds)
    {
        var clock = CreateClock();
        var mappedTracker = new DistanceTracker(timeProvider: clock);
        var utcTracker = new DistanceTracker(timeProvider: clock);
        _nanoseconds += 2_500_000_000;
        DateTimeOffset reportedGpsUtc = ManualTimeProvider.Epoch.AddSeconds(2.5 + gpsSkewSeconds);
        var utcSample = new GeoCoordinate(30, 120, timestamp: reportedGpsUtc, horizontalAccuracyMeters: 5);

        Assert.IsFalse(utcTracker.AddLocation(utcSample), "The raw GPS UTC demonstrates the clock-skew regression.");
        Assert.IsTrue(mappedTracker.AddLocation(Point(clock, _nanoseconds), out var result));

        Assert.AreEqual(FilterResult.Accept, result);
        Assert.AreEqual(ManualTimeProvider.Epoch.AddMilliseconds(2500), mappedTracker.Trajectory[0].Timestamp);
    }

    [TestMethod]
    public void GetCurrentSpeed_UsesSameMonotonicClock_ExpiresAtThreeSeconds()
    {
        var clock = CreateClock();
        var tracker = new DistanceTracker(timeProvider: clock);
        Assert.IsTrue(tracker.AddLocation(Point(clock, _nanoseconds, speed: 3)));

        _nanoseconds += 2_999_999_900;
        Assert.AreEqual(3, tracker.GetCurrentSpeedMps());
        _nanoseconds += 100;

        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        Assert.IsFalse(tracker.AddLocation(Point(clock, _nanoseconds, speed: 2.5)));
        Assert.AreEqual(2.5, tracker.GetCurrentSpeedMps());
    }

    private SensorClock CreateClock() => new(() => _nanoseconds, ManualTimeProvider.Epoch);

    private static GeoCoordinate Point(SensorClock clock, long timestampNanoseconds, double? speed = null) =>
        new(30, 120, timestamp: clock.GetSampleTime(timestampNanoseconds),
            horizontalAccuracyMeters: 5, speedMetersPerSecond: speed);
}

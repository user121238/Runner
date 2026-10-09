using Runner.Core.Geolocation;

namespace Runner.Core.Tests.Geolocation;

[TestClass]
public sealed class DistanceTrackerTests
{
    private readonly ManualTimeProvider _clock = new();

    [TestMethod]
    public void NewTracker_HasNoDistanceSpeedOrTrajectory()
    {
        var tracker = CreateTracker();

        Assert.AreEqual(0, tracker.TotalDistanceMeters);
        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        Assert.AreEqual(0, tracker.GetAverageSpeedMps());
        Assert.AreEqual(0, tracker.TrajectoryPointCount);
        Assert.IsEmpty(tracker.Trajectory);
    }

    [TestMethod]
    public void AddLocation_StraightRun_AccumulatesDistanceAndSpeedInCorrectUnits()
    {
        var tracker = CreateTracker();
        for (int time = 0; time <= 30; time++)
            Assert.IsTrue(GeoTestData.Add(tracker, _clock, time * 3, time));

        Assert.AreEqual(90, tracker.TotalDistanceMeters, 1e-8);
        Assert.AreEqual(0.09, tracker.TotalDistanceKm, 1e-10);
        Assert.AreEqual(3, tracker.GetCurrentSpeedMps(), 1e-8);
        Assert.AreEqual(10.8, tracker.GetCurrentSpeedKmh(), 1e-8);
        Assert.AreEqual(3, tracker.GetAverageSpeedMps(), 1e-8);
        Assert.AreEqual(10.8, tracker.GetAverageSpeedKmh(), 1e-8);
        Assert.AreEqual(31, tracker.TrajectoryPointCount);
        Assert.HasCount(31, tracker.Trajectory);
    }

    [TestMethod]
    public void AddLocation_StationaryFourMeterJitterWithinFiveMeterAccuracy_AddsZeroDistance()
    {
        var tracker = CreateTracker();
        for (int time = 0; time <= 60; time += 2)
            GeoTestData.Add(tracker, _clock, time % 4 == 0 ? -4 : 4, time, accuracy: 5);

        Assert.AreEqual(0, tracker.TotalDistanceMeters);
        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
    }

    [TestMethod]
    public void GetCurrentSpeed_ThreeSecondsWithoutNewObservation_Expires()
    {
        var tracker = MovingTracker();

        _clock.SetSeconds(3.999);
        Assert.AreEqual(3, tracker.GetCurrentSpeedMps(), 1e-8);
        _clock.SetSeconds(4);
        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        _clock.SetSeconds(30);
        Assert.AreEqual(0, tracker.GetCurrentSpeedKmh());
    }

    [TestMethod]
    public void AddLocation_StationaryFreshPoint_ClearsOldMovingSpeedImmediately()
    {
        var tracker = MovingTracker();

        Assert.IsFalse(GeoTestData.Add(tracker, _clock, 3, 2));

        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        Assert.AreEqual(3, tracker.TotalDistanceMeters, 1e-8);
    }

    [TestMethod]
    public void AddLocation_SubthresholdMovementWithNativeSpeed_UpdatesSpeedWithoutAddingDistance()
    {
        var tracker = CreateTracker();
        GeoTestData.Add(tracker, _clock, 0, 0, accuracy: 5, speed: 3);

        Assert.IsFalse(GeoTestData.Add(tracker, _clock, 3, 1, accuracy: 5, speed: 2.5));

        Assert.AreEqual(2.5, tracker.GetCurrentSpeedMps());
        Assert.AreEqual(0, tracker.TotalDistanceMeters);
    }

    [TestMethod]
    public void AddLocation_ValidNativeSpeed_TakesPrecedenceOverCoordinateSpeed()
    {
        var tracker = CreateTracker();
        GeoTestData.Add(tracker, _clock, 0, 0);
        GeoTestData.Add(tracker, _clock, 3, 1, speed: 2.7);

        Assert.AreEqual(2.7, tracker.GetCurrentSpeedMps());
        Assert.AreEqual(3, tracker.TotalDistanceMeters, 1e-8);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(-1.0)]
    [DataRow(100.0)]
    public void AddLocation_InvalidNativeSpeed_DoesNotPoisonStationarySpeed(double speed)
    {
        var tracker = MovingTracker();

        GeoTestData.Add(tracker, _clock, 3, 2, speed: speed);

        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        Assert.AreEqual(3, tracker.TotalDistanceMeters, 1e-8);
    }

    [TestMethod]
    public void BeginSegment_PauseWithHundredMeterRelocation_DoesNotBridgePause()
    {
        var tracker = CreateTracker();
        GeoTestData.Add(tracker, _clock, 0, 0);
        GeoTestData.Add(tracker, _clock, 10, 5);

        tracker.BeginSegment();

        Assert.AreEqual(10, tracker.TotalDistanceMeters, 1e-8);
        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        Assert.IsTrue(GeoTestData.Add(tracker, _clock, 110, 65));
        Assert.AreEqual(10, tracker.TotalDistanceMeters, 1e-8);
        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        GeoTestData.Add(tracker, _clock, 120, 70);
        Assert.AreEqual(20, tracker.TotalDistanceMeters, 1e-8);
        Assert.AreEqual(2, tracker.GetAverageSpeedMps(), 1e-8,
            "The 60 s pause must not enter active segment elapsed time.");
    }

    [TestMethod]
    public void AddLocation_GapLongerThanFifteenSeconds_StartsAnotherSegment()
    {
        var tracker = CreateTracker();
        GeoTestData.Add(tracker, _clock, 0, 0);
        GeoTestData.Add(tracker, _clock, 10, 5);

        Assert.IsTrue(GeoTestData.Add(tracker, _clock, 110, 21));

        Assert.AreEqual(10, tracker.TotalDistanceMeters, 1e-8);
        GeoTestData.Add(tracker, _clock, 120, 26);
        Assert.AreEqual(20, tracker.TotalDistanceMeters, 1e-8);
        Assert.AreEqual(2, tracker.GetAverageSpeedMps(), 1e-8);
    }

    [TestMethod]
    public void AddLocation_GapBelowLimit_KeepsSameSegment()
    {
        var tracker = CreateTracker();
        GeoTestData.Add(tracker, _clock, 0, 0);
        GeoTestData.Add(tracker, _clock, 42, 14);

        Assert.AreEqual(42, tracker.TotalDistanceMeters, 1e-8);
        Assert.AreEqual(3, tracker.GetAverageSpeedMps(), 1e-8);
    }

    [TestMethod]
    public void AddLocation_OldCachedPoint_IsRejectedBeforeItBecomesAnAnchor()
    {
        var tracker = CreateTracker();
        _clock.SetSeconds(11);

        Assert.IsFalse(tracker.AddLocation(GeoTestData.Point(0, 0)));
        Assert.AreEqual(0, tracker.TrajectoryPointCount);
        Assert.IsTrue(tracker.AddLocation(GeoTestData.Point(100, 11)));
        Assert.AreEqual(0, tracker.TotalDistanceMeters);
    }

    [TestMethod]
    public void AddLocation_FuturePoint_IsRejectedBeforeItBecomesAnAnchor()
    {
        var tracker = CreateTracker();

        Assert.IsFalse(tracker.AddLocation(GeoTestData.Point(100, 10)));
        Assert.AreEqual(0, tracker.TrajectoryPointCount);
        Assert.IsTrue(tracker.AddLocation(GeoTestData.Point(0, 0)));
        Assert.AreEqual(0, tracker.TotalDistanceMeters);
    }

    [TestMethod]
    public void AddLocation_NaNInMovingTrack_IsRejectedWithoutContaminatingDistance()
    {
        var tracker = MovingTracker();
        _clock.SetSeconds(2);
        var invalid = new GeoCoordinate(double.NaN, 0, timestamp: _clock.GetUtcNow());

        Assert.IsFalse(tracker.AddLocation(invalid, out var reason));
        Assert.AreNotEqual(FilterResult.Accept, reason);
        Assert.AreEqual(3, tracker.TotalDistanceMeters, 1e-8);
        Assert.IsTrue(GeoTestData.Add(tracker, _clock, 9, 3));
        Assert.AreEqual(9, tracker.TotalDistanceMeters, 1e-8);
    }

    [TestMethod]
    public void AddLocation_PoorAccuracy_DoesNotRefreshStaleVelocity()
    {
        var tracker = MovingTracker();
        GeoTestData.Add(tracker, _clock, 6, 3, accuracy: 100, speed: 3);
        _clock.SetSeconds(4);

        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        Assert.AreEqual(3, tracker.TotalDistanceMeters, 1e-8);
    }

    [TestMethod]
    public void Reset_MovingTrack_ClearsDistanceTrajectorySpeedAndTimeState()
    {
        var tracker = MovingTracker();

        tracker.Reset();

        Assert.AreEqual(0, tracker.TotalDistanceMeters);
        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        Assert.AreEqual(0, tracker.GetAverageSpeedMps());
        Assert.AreEqual(0, tracker.TrajectoryPointCount);
        Assert.IsEmpty(tracker.Trajectory);
        Assert.IsTrue(GeoTestData.Add(tracker, _clock, 1000, 0));
        Assert.IsTrue(GeoTestData.Add(tracker, _clock, 1003, 1));
        Assert.AreEqual(3, tracker.TotalDistanceMeters, 1e-8);
    }

    [TestMethod]
    public void Trajectory_ReturnsSnapshotUnaffectedByLaterPointsAndReset()
    {
        var tracker = MovingTracker();
        IReadOnlyList<GeoCoordinate> snapshot = tracker.Trajectory;

        GeoTestData.Add(tracker, _clock, 6, 2);
        tracker.Reset();

        Assert.HasCount(2, snapshot);
        Assert.AreEqual(GeoTestData.Point(0, 0), snapshot[0]);
        Assert.AreEqual(GeoTestData.Point(3, 1), snapshot[1]);
        Assert.IsEmpty(tracker.Trajectory);
    }

    [TestMethod]
    public void GetAverageSpeed_StationaryObservationsWithinSegment_IncludeStationaryElapsedTime()
    {
        var tracker = CreateTracker();
        GeoTestData.Add(tracker, _clock, 0, 0);
        GeoTestData.Add(tracker, _clock, 6, 2);
        GeoTestData.Add(tracker, _clock, 6, 4);
        GeoTestData.Add(tracker, _clock, 6, 6);

        Assert.AreEqual(6, tracker.TotalDistanceMeters, 1e-8);
        Assert.AreEqual(1, tracker.GetAverageSpeedMps(), 1e-8);
    }

    [TestMethod]
    public void AddLocation_ExactlyTenSecondsOld_IsAcceptedAtConfiguredBoundary()
    {
        var tracker = CreateTracker();
        _clock.SetSeconds(10);

        Assert.IsTrue(tracker.AddLocation(GeoTestData.Point(0, 0)));
    }

    [TestMethod]
    public void AddLocation_ExactlyFifteenSecondGap_RemainsInSameSegment()
    {
        var tracker = CreateTracker();
        GeoTestData.Add(tracker, _clock, 0, 0);
        GeoTestData.Add(tracker, _clock, 45, 15);

        Assert.AreEqual(45, tracker.TotalDistanceMeters, 1e-8);
        Assert.AreEqual(3, tracker.GetAverageSpeedMps(), 1e-8);
    }

    [TestMethod]
    public void GetCurrentSpeed_ClockMovesBeforeLastObservation_ReturnsZero()
    {
        var tracker = MovingTracker();
        _clock.SetSeconds(0);

        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
    }

    [TestMethod]
    public void GetCurrentSpeed_ExpiredSpeedRead_DoesNotDiscardDistanceAnchorBeforeFreshFix()
    {
        var tracker = MovingTracker();
        _clock.SetSeconds(4);
        Assert.AreEqual(0, tracker.GetCurrentSpeedMps());
        Assert.AreEqual(0, tracker.GetCurrentSpeedKmh());

        Assert.IsTrue(GeoTestData.Add(tracker, _clock, 15, 5));

        Assert.AreEqual(15, tracker.TotalDistanceMeters, 1e-8);
        Assert.AreEqual(3, tracker.GetCurrentSpeedMps(), 1e-8);
        Assert.AreEqual(3, tracker.GetAverageSpeedMps(), 1e-8);
    }

    private DistanceTracker CreateTracker() => new(null, _clock);

    private DistanceTracker MovingTracker()
    {
        var tracker = CreateTracker();
        GeoTestData.Add(tracker, _clock, 0, 0);
        GeoTestData.Add(tracker, _clock, 3, 1);
        return tracker;
    }
}

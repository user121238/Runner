using Runner.Core.Session;
using Runner.Core.Tests.Geolocation;

namespace Runner.Core.Tests.Session;

[TestClass]
public sealed class RunSessionTests
{
    private readonly ManualTimeProvider _clock = new();

    private RunSession StartedSession(int targetCadence = 180)
    {
        var session = new RunSession(_clock);
        session.Start(targetCadence);
        return session;
    }

    [TestMethod]
    public void NewSession_IsIdleWithZeroMetrics()
    {
        var session = new RunSession(_clock);

        Assert.AreEqual(RunSessionState.Idle, session.State);
        Assert.IsTrue(session.CanStart);
        Assert.IsFalse(session.CanPause);
        Assert.IsFalse(session.CanResume);
        Assert.IsFalse(session.CanStop);
        Assert.AreEqual(TimeSpan.Zero, session.Elapsed);
        Assert.AreEqual(0, session.DistanceMeters);
        Assert.AreEqual(0, session.StepCount);
        Assert.AreEqual(0, session.CurrentCadence);
        Assert.AreEqual(0, session.AverageCadence);
        Assert.AreEqual(0, session.AverageSpeedKmh);
        Assert.IsNull(session.StartTime);
        Assert.IsNull(session.EndTime);
        Assert.IsEmpty(session.TrackPoints);
        Assert.IsEmpty(session.CadenceSamples);
    }

    [TestMethod]
    public void Start_RecordsStartTimeTargetAndCadenceAvailability()
    {
        _clock.SetSeconds(5);
        var session = new RunSession(_clock);

        session.Start(170, cadenceAvailable: false);

        Assert.AreEqual(RunSessionState.Running, session.State);
        Assert.AreEqual(ManualTimeProvider.Epoch.AddSeconds(5), session.StartTime);
        Assert.AreEqual(170, session.TargetCadence);
        Assert.IsFalse(session.CadenceAvailable);
        Assert.IsFalse(session.CanStart);
        Assert.IsTrue(session.CanPause);
        Assert.IsFalse(session.CanResume);
        Assert.IsTrue(session.CanStop);
    }

    [TestMethod]
    public void Start_NegativeTargetCadence_Throws()
    {
        var session = new RunSession(_clock);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Start(-1));
        Assert.AreEqual(RunSessionState.Idle, session.State);
    }

    [TestMethod]
    public void Start_ZeroTargetCadence_MeansNoTarget()
    {
        var session = new RunSession(_clock);

        session.Start(0);

        Assert.AreEqual(0, session.TargetCadence);
        Assert.AreEqual(RunSessionState.Running, session.State);
    }

    [TestMethod]
    public void UpdateDistance_RecordsSamplesOnlyWhileRunning_AndDrivesCurrentPace()
    {
        var session = StartedSession();
        Assert.IsEmpty(session.DistanceSamples);
        Assert.IsNull(session.CurrentPace);

        _clock.SetSeconds(10);
        session.UpdateDistance(25);
        _clock.SetSeconds(20);
        session.UpdateDistance(50);
        _clock.SetSeconds(30);
        session.UpdateDistance(75);

        Assert.AreEqual(TimeSpan.FromSeconds(400), session.CurrentPace);

        // 暂停期间的距离被忽略，不产生采样；暂停不计入跑动时长
        session.Pause();
        session.UpdateDistance(500);
        Assert.AreEqual(75, session.DistanceMeters);
        _clock.SetSeconds(40);
        session.Resume();
        _clock.SetSeconds(50);
        session.UpdateDistance(100);

        var samples = session.DistanceSamples;
        Assert.HasCount(4, samples);
        Assert.AreEqual(new DistanceSample(TimeSpan.FromSeconds(10), 25), samples[0]);
        Assert.AreEqual(new DistanceSample(TimeSpan.FromSeconds(40), 100), samples[3]);

        // 20 秒没有新的距离采样：人站着，实时配速不显示
        _clock.SetSeconds(70);
        Assert.IsNull(session.CurrentPace);
    }

    [TestMethod]
    public void SetTargetCadence_UpdatesWhileActiveAndRejectsAfterStop()
    {
        var session = StartedSession();

        session.SetTargetCadence(170);
        Assert.AreEqual(170, session.TargetCadence);

        session.Pause();
        session.SetTargetCadence(0);
        Assert.AreEqual(0, session.TargetCadence);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.SetTargetCadence(-5));
        Assert.AreEqual(0, session.TargetCadence);

        session.Stop();
        Assert.ThrowsExactly<InvalidOperationException>(() => session.SetTargetCadence(180));
    }

    [TestMethod]
    public void Elapsed_CountsOnlyRunningTime()
    {
        var session = StartedSession();
        _clock.SetSeconds(10);
        Assert.AreEqual(TimeSpan.FromSeconds(10), session.Elapsed);

        session.Pause();
        _clock.SetSeconds(25);
        Assert.AreEqual(TimeSpan.FromSeconds(10), session.Elapsed);

        session.Resume();
        _clock.SetSeconds(40);
        Assert.AreEqual(TimeSpan.FromSeconds(25), session.Elapsed);

        session.Stop();
        _clock.SetSeconds(100);
        Assert.AreEqual(TimeSpan.FromSeconds(25), session.Elapsed);
        Assert.AreEqual(ManualTimeProvider.Epoch.AddSeconds(40), session.EndTime);
        Assert.AreEqual(RunSessionState.Stopped, session.State);
    }

    [TestMethod]
    public void Stop_WhilePaused_KeepsElapsedAndEndsSession()
    {
        var session = StartedSession();
        _clock.SetSeconds(30);
        session.Pause();
        _clock.SetSeconds(50);

        session.Stop();

        Assert.AreEqual(TimeSpan.FromSeconds(30), session.Elapsed);
        Assert.AreEqual(ManualTimeProvider.Epoch.AddSeconds(50), session.EndTime);
        Assert.IsFalse(session.CanStart);
        Assert.IsFalse(session.CanStop);
    }

    [TestMethod]
    public void InvalidTransitions_ThrowWithoutChangingState()
    {
        var session = new RunSession(_clock);
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Pause());
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Resume());
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Stop());
        Assert.AreEqual(RunSessionState.Idle, session.State);

        session.Start(180);
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Start(180));
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Resume());
        Assert.AreEqual(RunSessionState.Running, session.State);

        session.Pause();
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Pause());
        Assert.AreEqual(RunSessionState.Paused, session.State);

        session.Stop();
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Start(180));
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Resume());
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Stop());
        Assert.AreEqual(RunSessionState.Stopped, session.State);
    }

    [TestMethod]
    public void Inputs_AreIgnoredUnlessRunning()
    {
        var session = new RunSession(_clock);
        var point = GeoTestData.Point(0, 0);

        session.UpdateDistance(100);
        session.UpdateStepCount(10);
        session.UpdateCadence(180);
        session.AddTrackPoint(point);
        Assert.AreEqual(0, session.DistanceMeters);
        Assert.AreEqual(0, session.StepCount);
        Assert.AreEqual(0, session.CurrentCadence);
        Assert.IsEmpty(session.TrackPoints);
        Assert.IsEmpty(session.CadenceSamples);

        session.Start(180);
        session.UpdateDistance(100);
        session.UpdateStepCount(10);
        session.UpdateCadence(180);
        session.AddTrackPoint(point);
        Assert.AreEqual(100, session.DistanceMeters);
        Assert.AreEqual(10, session.StepCount);
        Assert.AreEqual(180, session.CurrentCadence);
        Assert.HasCount(1, session.TrackPoints);
        Assert.HasCount(1, session.CadenceSamples);

        session.Pause();
        session.UpdateDistance(200);
        session.UpdateStepCount(20);
        session.UpdateCadence(170);
        session.AddTrackPoint(point);
        Assert.AreEqual(100, session.DistanceMeters);
        Assert.AreEqual(10, session.StepCount);
        Assert.AreEqual(0, session.CurrentCadence);
        Assert.HasCount(1, session.TrackPoints);
        Assert.HasCount(1, session.CadenceSamples);

        session.Resume();
        session.UpdateDistance(200);
        Assert.AreEqual(200, session.DistanceMeters);

        session.Stop();
        session.UpdateDistance(300);
        session.UpdateStepCount(30);
        Assert.AreEqual(200, session.DistanceMeters);
        Assert.AreEqual(10, session.StepCount);
    }

    [TestMethod]
    public void PauseAndStop_ClearCurrentCadence()
    {
        var session = StartedSession();
        session.UpdateCadence(180);

        session.Pause();
        Assert.AreEqual(0, session.CurrentCadence);

        session.Resume();
        Assert.AreEqual(0, session.CurrentCadence);
        session.UpdateCadence(175);
        Assert.AreEqual(175, session.CurrentCadence);

        session.Stop();
        Assert.AreEqual(0, session.CurrentCadence);
    }

    [TestMethod]
    public void AverageCadence_IsStepsPerRunningMinute()
    {
        var session = StartedSession();
        _clock.SetSeconds(60);
        session.UpdateStepCount(180);
        session.Pause();
        _clock.SetSeconds(120);
        session.Resume();
        _clock.SetSeconds(180);
        session.UpdateStepCount(400);

        Assert.AreEqual(200, session.AverageCadence, 1e-9);

        session.Stop();
        _clock.SetSeconds(600);
        Assert.AreEqual(200, session.AverageCadence, 1e-9);
    }

    [TestMethod]
    public void AverageSpeed_IsDistanceOverRunningTime()
    {
        var session = StartedSession();
        _clock.SetSeconds(300);
        session.UpdateDistance(1000);

        Assert.AreEqual(12, session.AverageSpeedKmh, 1e-9);
        Assert.AreEqual(1, session.DistanceKm, 1e-12);
    }

    [TestMethod]
    public void CadenceSamples_UseClockTimeAndReturnSnapshot()
    {
        var session = StartedSession();
        _clock.SetSeconds(1);
        session.UpdateCadence(160);
        _clock.SetSeconds(2);
        session.UpdateCadence(170);

        var snapshot = session.CadenceSamples;
        _clock.SetSeconds(3);
        session.UpdateCadence(180);

        Assert.HasCount(2, snapshot);
        Assert.AreEqual(new CadenceSample(ManualTimeProvider.Epoch.AddSeconds(1), 160), snapshot[0]);
        Assert.AreEqual(new CadenceSample(ManualTimeProvider.Epoch.AddSeconds(2), 170), snapshot[1]);
        Assert.HasCount(3, session.CadenceSamples);
    }

    [TestMethod]
    public void TrackPoints_ReturnSnapshot()
    {
        var session = StartedSession();
        session.AddTrackPoint(GeoTestData.Point(0, 0));

        var snapshot = session.TrackPoints;
        session.AddTrackPoint(GeoTestData.Point(3, 1));

        Assert.HasCount(1, snapshot);
        Assert.HasCount(2, session.TrackPoints);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(-1.0)]
    public void UpdateDistance_InvalidValue_Throws(double totalMeters)
    {
        var session = StartedSession();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.UpdateDistance(totalMeters));
        Assert.AreEqual(0, session.DistanceMeters);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-0.5)]
    public void UpdateCadence_InvalidValue_Throws(double stepsPerMinute)
    {
        var session = StartedSession();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.UpdateCadence(stepsPerMinute));
        Assert.AreEqual(0, session.CurrentCadence);
        Assert.IsEmpty(session.CadenceSamples);
    }

    [TestMethod]
    public void UpdateStepCount_Negative_Throws()
    {
        var session = StartedSession();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.UpdateStepCount(-1));
        Assert.AreEqual(0, session.StepCount);
    }
}

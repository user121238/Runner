using Runner.Core.Activities;
using Runner.Core.Session;
using Runner.Core.Tests.Geolocation;

namespace Runner.Core.Tests.Activities;

[TestClass]
public sealed class ActivityRecordTests
{
    private readonly ManualTimeProvider _clock = new();

    [TestMethod]
    public void FromSession_MapsAllFields()
    {
        var session = new RunSession(_clock);
        session.Start(180);
        session.AddTrackPoint(GeoTestData.Point(0, 0, accuracy: 5));
        _clock.SetSeconds(10);
        session.UpdateCadence(170);
        _clock.SetSeconds(300);
        session.UpdateDistance(1000);
        session.UpdateStepCount(900);
        session.AddTrackPoint(GeoTestData.Point(1000, 300));
        session.Stop();

        var record = ActivityRecord.FromSession(session, "法特莱克");

        Assert.AreEqual(ManualTimeProvider.Epoch.UtcDateTime, record.StartTimeUtc);
        Assert.AreEqual(ManualTimeProvider.Epoch.AddSeconds(300).UtcDateTime, record.EndTimeUtc);
        Assert.AreEqual(300, record.ElapsedSeconds);
        Assert.AreEqual(1000, record.DistanceMeters);
        Assert.AreEqual(900, record.StepCount);
        Assert.AreEqual(180, record.AverageCadence, 1e-9);
        Assert.AreEqual(12, record.AverageSpeedKmh, 1e-9);
        Assert.AreEqual("法特莱克", record.WorkoutName);
        Assert.HasCount(2, record.TrackPoints);
        Assert.AreEqual(5, record.TrackPoints[0].HorizontalAccuracyMeters);
        Assert.AreEqual(ManualTimeProvider.Epoch.AddSeconds(300).UtcDateTime, record.TrackPoints[1].TimestampUtc);
        Assert.HasCount(1, record.CadencePoints);
        Assert.AreEqual(170, record.CadencePoints[0].StepsPerMinute);
        Assert.AreEqual(ManualTimeProvider.Epoch.AddSeconds(10).UtcDateTime, record.CadencePoints[0].TimeUtc);
        Assert.HasCount(1, record.DistanceSamples);
        Assert.AreEqual(300, record.DistanceSamples[0].ElapsedSeconds);
        Assert.AreEqual(1000, record.DistanceSamples[0].Meters);
        Assert.AreEqual(new DistanceSample(TimeSpan.FromSeconds(300), 1000), record.DistanceSamples[0].ToSample());
    }

    [TestMethod]
    public void FromSession_RequiresStoppedSession()
    {
        var session = new RunSession(_clock);
        session.Start(180);

        Assert.ThrowsExactly<InvalidOperationException>(() => ActivityRecord.FromSession(session, null));
    }

    [TestMethod]
    public void ShouldRecord_OnlyStoppedSessionsOfAtLeastFiveSeconds()
    {
        var session = new RunSession(_clock);
        Assert.IsFalse(ActivityRecord.ShouldRecord(session));
        session.Start(180);
        _clock.SetSeconds(10);
        Assert.IsFalse(ActivityRecord.ShouldRecord(session));
        session.Stop();
        Assert.IsTrue(ActivityRecord.ShouldRecord(session));

        var brief = new RunSession(_clock);
        brief.Start(0);
        _clock.SetSeconds(14);
        brief.Stop();
        Assert.IsFalse(ActivityRecord.ShouldRecord(brief));

        var exact = new RunSession(_clock);
        exact.Start(0);
        _clock.SetSeconds(19);
        exact.Stop();
        Assert.IsTrue(ActivityRecord.ShouldRecord(exact));
    }
}

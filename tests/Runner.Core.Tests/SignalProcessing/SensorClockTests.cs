using Runner.Core.SignalProcessing;

namespace Runner.Core.Tests.SignalProcessing;

[TestClass]
public sealed class SensorClockTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Constructor_NullNanosecondSource_RejectsNamedArgument()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new SensorClock(null!, Anchor));

        Assert.AreEqual("nanoseconds", exception.ParamName);
    }

    [TestMethod]
    public void GetSampleTime_NonZeroDeviceUptime_MapsAnchorAndCurrentSampleToUtc()
    {
        long nanoseconds = 259_200_000_000_000; // Device has already been running for three days.
        var clock = new SensorClock(() => nanoseconds, Anchor);
        Assert.AreEqual(Anchor, clock.GetUtcNow());
        Assert.AreEqual(Anchor, clock.GetSampleTime(nanoseconds));

        nanoseconds += 2_500_000_000;

        Assert.AreEqual(Anchor.AddMilliseconds(2500), clock.GetUtcNow());
        Assert.AreEqual(Anchor.AddMilliseconds(2500), clock.GetSampleTime(nanoseconds));
    }

    [TestMethod]
    public void GetSampleTime_DelayedSample_PreservesCaptureTimeRatherThanArrivalTime()
    {
        long nanoseconds = 100_000_000_000;
        var clock = new SensorClock(() => nanoseconds, Anchor);
        long capturedAt = nanoseconds + 1_000_000_000;
        nanoseconds += 3_000_000_000;

        Assert.AreEqual(Anchor.AddSeconds(1), clock.GetSampleTime(capturedAt));
        Assert.AreEqual(TimeSpan.FromSeconds(2), clock.GetUtcNow() - clock.GetSampleTime(capturedAt));
    }

    [TestMethod]
    public void GetSampleTime_SampleCapturedBeforeClockConstruction_MapsBeforeAnchor()
    {
        long nanoseconds = 100_000_000_000;
        var clock = new SensorClock(() => nanoseconds, Anchor);

        Assert.AreEqual(Anchor.AddSeconds(-2), clock.GetSampleTime(98_000_000_000));
    }

    [TestMethod]
    [DataRow(100L, 1L)]
    [DataRow(123_400L, 1234L)]
    [DataRow(-123_400L, -1234L)]
    [DataRow(999_900L, 9999L)]
    public void GetSampleTime_SubMillisecondDelta_PreservesDateTimeTickPrecision(long deltaNanoseconds, long ticks)
    {
        long nanoseconds = 123_000_000_000;
        var clock = new SensorClock(() => nanoseconds, Anchor);

        Assert.AreEqual(Anchor.AddTicks(ticks), clock.GetSampleTime(nanoseconds + deltaNanoseconds));
    }

    [TestMethod]
    public void GetTimestamp_UsesNanosecondUnitsConsistentWithElapsedTime()
    {
        long nanoseconds = 300_000_000_000;
        var clock = new SensorClock(() => nanoseconds, Anchor);
        long first = clock.GetTimestamp();

        nanoseconds += 1_234_567_800;

        Assert.AreEqual(1_000_000_000L, clock.TimestampFrequency);
        Assert.AreEqual(nanoseconds, clock.GetTimestamp());
        Assert.AreEqual(TimeSpan.FromTicks(12_345_678), clock.GetElapsedTime(first, clock.GetTimestamp()));
        Assert.AreEqual(TimeSpan.FromTicks(12_345_678), clock.GetElapsedTime(first));
    }

    [TestMethod]
    public void GetUtcNow_RepeatedReads_DoNotReanchorPreviouslyCapturedSamples()
    {
        long nanoseconds = 100_000_000_000;
        var clock = new SensorClock(() => nanoseconds, Anchor);
        long capturedAt = nanoseconds + 1_000_000_000;
        nanoseconds += 2_000_000_000;
        Assert.AreEqual(Anchor.AddSeconds(2), clock.GetUtcNow());
        nanoseconds += 3_000_000_000;

        Assert.AreEqual(Anchor.AddSeconds(5), clock.GetUtcNow());
        Assert.AreEqual(Anchor.AddSeconds(1), clock.GetSampleTime(capturedAt));
    }
}

using Runner.Core.SignalProcessing;

namespace Runner.Core.Tests.SignalProcessing;

[TestClass]
public sealed class AccelerometerStepDetectorTests
{
    public static IEnumerable<(int cadence, int sampleRate)> CadencesAndSampleRates =>
        from cadence in new[] { 90, 120, 180, 210, 240 }
        from sampleRate in new[] { 25, 50, 100 }
        select (cadence, sampleRate);

    [TestMethod]
    [DynamicData(nameof(CadencesAndSampleRates))]
    public void ProcessSample_KnownCycles_CountsStepsAcrossCadenceAndSampleRate(int cadence, int sampleRate)
    {
        var detector = new AccelerometerStepDetector();

        int count = RunSignal(detector, cadence, sampleRate);

        // A partial first/last oscillation may fall outside the detector's settling window.
        int expected = cadence / 3;
        Assert.IsInRange(expected - 1, expected + 1, count,
            $"20 s at {cadence} SPM / {sampleRate} Hz should yield {expected} cycles.");
    }

    [TestMethod]
    public void ProcessSample_StationaryWithSmallDeterministicNoise_ProducesNoSteps()
    {
        var detector = new AccelerometerStepDetector();
        int count = 0;
        for (long time = 0; time < 60000; time += 20)
        {
            float noise = (float)(0.08 * Math.Sin(time * 0.071) + 0.05 * Math.Sin(time * 0.019));
            if (detector.ProcessSample(noise, -noise, 9.81f + noise, time))
                count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void ProcessSample_SameMotionOnDifferentAxes_CountsEqually()
    {
        int zAxisCount = RunSignal(new AccelerometerStepDetector(), 180, 50);
        int xAxisCount = RunSignal(new AccelerometerStepDetector(), 180, 50, xAxis: true);

        Assert.IsInRange(59, 61, zAxisCount);
        Assert.AreEqual(zAxisCount, xAxisCount);
    }

    [TestMethod]
    public void ProcessSample_IrregularSampling_CountsKnownCycles()
    {
        var detector = new AccelerometerStepDetector();
        int count = 0;
        int[] intervals = [10, 30, 15, 25, 20];
        int sample = 0;
        for (long time = 0; time < 23000; time += intervals[sample++ % intervals.Length])
        {
            if (detector.ProcessSample(0, 0, Signal(time, 180), time))
                count++;
        }

        Assert.IsInRange(59, 61, count);
    }

    [TestMethod]
    public void Reset_PreviousSession_ReproducesFirstSessionWithoutLeakedState()
    {
        var detector = new AccelerometerStepDetector();
        int first = RunSignal(detector, 210, 50);

        detector.Reset();
        int second = RunSignal(detector, 210, 50);

        Assert.IsInRange(69, 71, first);
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void ProcessSample_LongSamplingGap_ResumesWithoutPhantomStep()
    {
        var detector = new AccelerometerStepDetector();
        RunSignal(detector, 180, 50);

        Assert.IsFalse(detector.ProcessSample(0, 0, 9.81f, 60000));
        int resumed = RunSignal(detector, 180, 50, startTime: 60020);

        Assert.IsInRange(59, 61, resumed);
    }

    [TestMethod]
    public void ProcessSample_NonFiniteSamples_DoNotPoisonFollowingMotion()
    {
        var detector = new AccelerometerStepDetector();

        Assert.IsFalse(detector.ProcessSample(float.NaN, 0, 9.81f, 0));
        Assert.IsFalse(detector.ProcessSample(0, float.PositiveInfinity, 9.81f, 20));
        int count = RunSignal(detector, 180, 50, startTime: 100);

        Assert.IsInRange(59, 61, count);
    }

    [TestMethod]
    public void ProcessSample_DuplicateOutOfOrderAndInvalidSamples_DoNotInventStepsOrCorruptState()
    {
        var detector = new AccelerometerStepDetector();
        detector.ProcessSample(0, 0, 9.81f, 1000);

        Assert.IsFalse(detector.ProcessSample(0, 0, 30, 1000));
        Assert.IsFalse(detector.ProcessSample(0, 0, 30, 999));
        Assert.IsFalse(detector.ProcessSample(0, 0, 30, -1));
        Assert.IsFalse(detector.ProcessSample(0, 0, float.NaN, 1020));

        Assert.IsInRange(59, 61, RunSignal(detector, 180, 50, startTime: 1040));
    }

    private static int RunSignal(AccelerometerStepDetector detector, int cadence, int sampleRate,
        bool xAxis = false, long startTime = 0)
    {
        int count = 0;
        for (long time = 0; time < 23000; time += 1000 / sampleRate)
        {
            float value = Signal(time, cadence);
            bool detected = xAxis
                ? detector.ProcessSample(value, 0, 0, startTime + time)
                : detector.ProcessSample(0, 0, value, startTime + time);
            if (detected)
                count++;
        }
        return count;
    }

    private static float Signal(long time, int cadence) => time is >= 2000 and < 22000
        ? 9.81f + (float)(3 * Math.Sin(2 * Math.PI * cadence / 60.0 * (time - 2000) / 1000.0))
        : 9.81f;
}

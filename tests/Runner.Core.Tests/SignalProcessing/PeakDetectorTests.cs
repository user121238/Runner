using Runner.Core.SignalProcessing;

namespace Runner.Core.Tests.SignalProcessing;

[TestClass]
public sealed class PeakDetectorTests
{
    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Constructor_NonFiniteThreshold_IsRejected(float threshold)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PeakDetector(threshold, 200));
    }

    [TestMethod]
    public void Constructor_NegativeInterval_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PeakDetector(1, -1));
    }

    [TestMethod]
    public void DetectPeak_InvalidOrNonIncreasingSample_DoesNotCompletePendingPeak()
    {
        var detector = new PeakDetector(1, 200);
        detector.DetectPeak(2, 1000);

        Assert.IsFalse(detector.DetectPeak(0, 1000));
        Assert.IsFalse(detector.DetectPeak(0, 999));
        Assert.IsFalse(detector.DetectPeak(0, -1));
        Assert.IsFalse(detector.DetectPeak(float.NaN, 1010));
        Assert.IsTrue(detector.DetectPeak(0, 1020));
    }

    [TestMethod]
    public void DetectPeak_RiseAndFall_ReportsOnlyCompletedPeak()
    {
        var detector = new PeakDetector(1, 200);

        Assert.IsFalse(detector.DetectPeak(0, 0));
        Assert.IsFalse(detector.DetectPeak(2, 10));
        Assert.IsFalse(detector.DetectPeak(3, 20));
        Assert.IsFalse(detector.DetectPeak(2, 30));
        Assert.IsTrue(detector.DetectPeak(0, 40));
        Assert.IsFalse(detector.DetectPeak(0, 50));
    }

    [TestMethod]
    [DataRow(250)]
    [DataRow(286)]
    [DataRow(333)]
    public void DetectPeak_HighCadenceAboveRefractoryPeriod_CountsEveryPeak(int intervalMs)
    {
        var detector = new PeakDetector(1, 200);
        int count = 0;
        for (int i = 0; i < 20; i++)
        {
            detector.DetectPeak(2, 1000 + i * intervalMs);
            if (detector.DetectPeak(0, 1010 + i * intervalMs))
                count++;
        }

        Assert.AreEqual(20, count);
    }

    [TestMethod]
    public void DetectPeak_TooClosePeak_DoesNotMoveLastAcceptedPeak()
    {
        var detector = new PeakDetector(1, 200);
        detector.DetectPeak(2, 1000);
        Assert.IsTrue(detector.DetectPeak(0, 1010));
        detector.DetectPeak(2, 1100);
        Assert.IsFalse(detector.DetectPeak(0, 1110));
        detector.DetectPeak(2, 1200);

        Assert.IsTrue(detector.DetectPeak(0, 1210));
    }

    [TestMethod]
    public void Reset_PendingPeakAndPreviousTimestamp_AreCleared()
    {
        var detector = new PeakDetector(1, 200);
        detector.DetectPeak(2, 1000);
        detector.DetectPeak(0, 1010);
        detector.DetectPeak(2, 1200);

        detector.Reset();

        Assert.IsFalse(detector.DetectPeak(0, 0));
        detector.DetectPeak(2, 10);
        Assert.IsTrue(detector.DetectPeak(0, 20));
    }
}

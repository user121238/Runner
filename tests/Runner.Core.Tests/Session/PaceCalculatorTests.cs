using Runner.Core.Session;

namespace Runner.Core.Tests.Session;

[TestClass]
public sealed class PaceCalculatorTests
{
    private static readonly TimeSpan FortyS = TimeSpan.FromSeconds(400);   // 2.5 m/s = 6'40"/km
    private static readonly TimeSpan TwentyS = TimeSpan.FromSeconds(200);  // 5 m/s = 3'20"/km

    /// <summary>每秒一条采样，匀速 metersPerSecond，跑 seconds 秒，起点为 (startSeconds, startMeters)。</summary>
    private static List<DistanceSample> Constant(double metersPerSecond, int seconds, double startSeconds = 0, double startMeters = 0)
    {
        var list = new List<DistanceSample>();
        for (int i = 0; i <= seconds; i++)
            list.Add(new DistanceSample(TimeSpan.FromSeconds(startSeconds + i), startMeters + i * metersPerSecond));
        return list;
    }

    [TestMethod]
    public void Average_IsElapsedPerKilometer()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(5), PaceCalculator.Average(TimeSpan.FromMinutes(5), 1000));
        Assert.AreEqual(TimeSpan.FromMinutes(10), PaceCalculator.Average(TimeSpan.FromSeconds(300), 500));
        Assert.IsNull(PaceCalculator.Average(TimeSpan.FromMinutes(5), 9.9));
        Assert.IsNull(PaceCalculator.Average(TimeSpan.Zero, 1000));
    }

    [TestMethod]
    public void Current_ConstantSpeed_UsesLastWindow()
    {
        var samples = Constant(2.5, 120);

        Assert.AreEqual(FortyS, PaceCalculator.Current(samples, TimeSpan.FromSeconds(120)));
    }

    [TestMethod]
    public void Current_ReflectsRecentSpeedChange()
    {
        var samples = Constant(2.5, 60);
        samples.AddRange(Constant(5, 60, startSeconds: 60, startMeters: 150).Skip(1));

        // 最近 30 秒全是 5 m/s
        Assert.AreEqual(TwentyS, PaceCalculator.Current(samples, TimeSpan.FromSeconds(120)));
    }

    [TestMethod]
    public void Current_StaleSamples_ReturnsNull()
    {
        var samples = Constant(2.5, 60);

        Assert.IsNotNull(PaceCalculator.Current(samples, TimeSpan.FromSeconds(70)));
        Assert.IsNull(PaceCalculator.Current(samples, TimeSpan.FromSeconds(70.1)));
    }

    [TestMethod]
    public void Current_TooFewOrTooShort_ReturnsNull()
    {
        Assert.IsNull(PaceCalculator.Current(Array.Empty<DistanceSample>(), TimeSpan.Zero));
        Assert.IsNull(PaceCalculator.Current(Constant(2.5, 0), TimeSpan.Zero));
        // 跨度 3 秒不够
        Assert.IsNull(PaceCalculator.Current(Constant(2.5, 3), TimeSpan.FromSeconds(3)));
        // 20 秒只走了 4 米
        Assert.IsNull(PaceCalculator.Current(Constant(0.2, 20), TimeSpan.FromSeconds(20)));
    }

    [TestMethod]
    public void Current_SingleSampleInWindow_FallsBackOneSample()
    {
        var samples = new[]
        {
            new DistanceSample(TimeSpan.Zero, 0),
            new DistanceSample(TimeSpan.FromSeconds(40), 100)
        };

        Assert.AreEqual(FortyS, PaceCalculator.Current(samples, TimeSpan.FromSeconds(40)));
    }

    [TestMethod]
    public void Current_StandingOrCrawling_ReturnsNull()
    {
        Assert.IsNull(PaceCalculator.Current(Constant(0, 60), TimeSpan.FromSeconds(60)));
        // 0.5 m/s = 33'20"/km，慢于 20'/km 的站立阈值
        Assert.IsNull(PaceCalculator.Current(Constant(0.5, 60), TimeSpan.FromSeconds(60)));
    }

    [TestMethod]
    public void Summarize_ConstantSpeed_FastestEqualsSlowest()
    {
        var summary = PaceCalculator.Summarize(Constant(2.5, 200));

        Assert.AreEqual(FortyS, summary.Fastest);
        Assert.AreEqual(FortyS, summary.Slowest);
    }

    [TestMethod]
    public void Summarize_TwoPhases_FindsBothExtremes()
    {
        var samples = Constant(2.5, 100);
        samples.AddRange(Constant(5, 100, startSeconds: 100, startMeters: 250).Skip(1));

        var summary = PaceCalculator.Summarize(samples);

        Assert.AreEqual(TwentyS, summary.Fastest);
        Assert.AreEqual(FortyS, summary.Slowest);
    }

    [TestMethod]
    public void Summarize_LessThanOneWindow_IsEmpty()
    {
        var summary = PaceCalculator.Summarize(Constant(2.5, 39));

        Assert.IsNull(summary.Fastest);
        Assert.IsNull(summary.Slowest);
    }

    [TestMethod]
    public void Summarize_LongStop_NotCountedAsSlowest()
    {
        var samples = Constant(2.5, 100);
        samples.AddRange(Constant(0, 600, startSeconds: 100, startMeters: 250).Skip(1));
        samples.AddRange(Constant(2.5, 100, startSeconds: 700, startMeters: 250).Skip(1));

        var summary = PaceCalculator.Summarize(samples);

        Assert.AreEqual(FortyS, summary.Fastest);
        Assert.AreEqual(FortyS, summary.Slowest);
    }

    [TestMethod]
    public void Summarize_BriefHesitation_CountsTowardSlowest()
    {
        var samples = Constant(2.5, 100);
        samples.AddRange(Constant(0, 8, startSeconds: 100, startMeters: 250).Skip(1));
        samples.AddRange(Constant(2.5, 100, startSeconds: 108, startMeters: 250).Skip(1));

        var summary = PaceCalculator.Summarize(samples);

        Assert.AreEqual(FortyS, summary.Fastest);
        // 跨过 8 秒犹豫的 100 米窗：40 + 8 秒
        Assert.AreEqual(TimeSpan.FromSeconds(480), summary.Slowest);
    }

    [TestMethod]
    public void Summarize_CrawlingSlowerThanMovingThreshold_Excluded()
    {
        // 0.5 m/s = 33'20"/km，慢于 20'/km 的站立阈值：有距离但不算配速
        var summary = PaceCalculator.Summarize(Constant(0.5, 300));

        Assert.IsNull(summary.Fastest);
        Assert.IsNull(summary.Slowest);
    }

    [TestMethod]
    public void Summarize_UsesTightestWindow()
    {
        // 前 50 秒慢走 50 米（每 5 秒一条，不触发断段），随后 10 秒冲 100 米
        var samples = new List<DistanceSample>();
        for (int t = 0; t <= 50; t += 5) samples.Add(new DistanceSample(TimeSpan.FromSeconds(t), t));
        samples.Add(new DistanceSample(TimeSpan.FromSeconds(55), 100));
        samples.Add(new DistanceSample(TimeSpan.FromSeconds(60), 150));

        var summary = PaceCalculator.Summarize(samples);

        // 终点 150 米处最紧的 ≥100 米窗从 50 米起：100 米 / 10 秒，而不是被前 50 秒慢走稀释
        Assert.AreEqual(TimeSpan.FromSeconds(100), summary.Fastest);
        Assert.AreEqual(TimeSpan.FromSeconds(550), summary.Slowest);
    }

    [TestMethod]
    public void Summarize_ZeroSpanWindow_Skipped()
    {
        var samples = new[]
        {
            new DistanceSample(TimeSpan.FromSeconds(10), 0),
            new DistanceSample(TimeSpan.FromSeconds(10), 150)
        };

        var summary = PaceCalculator.Summarize(samples);

        Assert.IsNull(summary.Fastest);
        Assert.IsNull(summary.Slowest);
    }
}

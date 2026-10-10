using Runner.Core.Session;

namespace Runner.Core.Activities;

/// <summary>距离采样的持久化形态：跑动秒数与累计米数，供记录详情重算配速与将来画配速曲线。</summary>
public class DistancePoint
{
    public double ElapsedSeconds { get; set; }
    public double Meters { get; set; }

    public static DistancePoint From(DistanceSample sample) => new()
    {
        ElapsedSeconds = sample.Elapsed.TotalSeconds,
        Meters = sample.Meters
    };

    public DistanceSample ToSample() => new(TimeSpan.FromSeconds(ElapsedSeconds), Meters);
}

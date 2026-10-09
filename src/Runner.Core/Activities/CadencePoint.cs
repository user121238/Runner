using Runner.Core.Session;

namespace Runner.Core.Activities;

/// <summary>步频采样的持久化形态：时间存 UTC。</summary>
public class CadencePoint
{
    public DateTime TimeUtc { get; set; }
    public double StepsPerMinute { get; set; }

    public static CadencePoint From(CadenceSample sample) => new()
    {
        TimeUtc = sample.Time.UtcDateTime,
        StepsPerMinute = sample.StepsPerMinute
    };
}

namespace Runner.Core.Session;

/// <summary>某一跑动时刻的累计距离采样。配速只从这条可信的（已过滤的）距离序列推算，不用 GPS 瞬时速度。</summary>
public readonly record struct DistanceSample(TimeSpan Elapsed, double Meters);

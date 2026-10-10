namespace Runner.Core.Session;

/// <summary>一次跑步的最快 / 最慢配速（每公里用时）；样本不足一个距离窗时为 null。</summary>
public readonly record struct PaceSummary(TimeSpan? Fastest, TimeSpan? Slowest);

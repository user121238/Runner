namespace Runner.Core.Session;

/// <summary>
/// 配速（每公里用时）只从会话的累计距离序列推算，不用 GPS 瞬时速度——一个跳点就能造出假的"最快配速"。
/// 实时配速取最近一段时间窗的 Δt/Δd；最快 / 最慢取固定距离滑窗的极值，停留超过 <see cref="StaleAfter"/> 处断开成段，
/// 窗不跨段（等红灯不算最慢配速，几秒的犹豫算）；慢于 <see cref="MaxMovingPace"/> 视为没在动。
/// </summary>
public static class PaceCalculator
{
    /// <summary>实时配速的时间窗。</summary>
    public static readonly TimeSpan CurrentWindow = TimeSpan.FromSeconds(30);

    /// <summary>超过这么久没有前进：实时配速不显示（人站着或 GPS 断了）；最快 / 最慢的滑窗也在这里断段。</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(10);

    /// <summary>慢于 20 分钟 / 公里（3 km/h）视为站着：实时配速不显示，最慢配速也不计入。</summary>
    public static readonly TimeSpan MaxMovingPace = TimeSpan.FromMinutes(20);

    /// <summary>最快 / 最慢配速的距离滑窗（米）：太短会被 GPS 误差放大，太长抹平变速。</summary>
    public const double SummaryWindowMeters = 100;

    /// <summary>少于这个距离的窗不算配速，避免起步几米算出离谱值。</summary>
    public const double MinMeters = 10;

    private static readonly TimeSpan MinCurrentSpan = TimeSpan.FromSeconds(5);

    /// <summary>平均配速 = 跑动时长 / 距离；距离不足 <see cref="MinMeters"/> 或时长为零时为 null。</summary>
    public static TimeSpan? Average(TimeSpan elapsed, double meters) =>
        meters >= MinMeters && elapsed > TimeSpan.Zero ? PerKilometer(elapsed, meters) : null;

    /// <summary>
    /// 实时配速：以 now（会话跑动时长）为基准，取最近 <see cref="CurrentWindow"/> 内的采样算 Δt/Δd。
    /// 窗内只有一条采样时退一条保证有跨度；采样过旧、跨度或距离太短、慢于站立阈值都返回 null。
    /// </summary>
    public static TimeSpan? Current(IReadOnlyList<DistanceSample> samples, TimeSpan now)
    {
        if (samples.Count < 2) return null;
        var end = samples[^1];
        if (now - end.Elapsed > StaleAfter) return null;

        var windowStart = end.Elapsed - CurrentWindow;
        int i = samples.Count - 1;
        while (i > 0 && samples[i - 1].Elapsed >= windowStart) i--;
        if (i == samples.Count - 1) i--;

        var start = samples[i];
        var span = end.Elapsed - start.Elapsed;
        double meters = end.Meters - start.Meters;
        if (span < MinCurrentSpan || meters < MinMeters) return null;

        var pace = PerKilometer(span, meters);
        return pace <= MaxMovingPace ? pace : null;
    }

    /// <summary>
    /// 最快 / 最慢配速：对每个"前进了"的采样取"刚好覆盖 ≥ <see cref="SummaryWindowMeters"/>"的最紧滑窗算配速，取极值。
    /// 上一次前进距今超过 <see cref="StaleAfter"/> 再动，就从这条采样重新起段，窗不跨段；不足一个窗为空。
    /// </summary>
    public static PaceSummary Summarize(IReadOnlyList<DistanceSample> samples)
    {
        TimeSpan? fastest = null, slowest = null;
        int start = 0;
        int lastProgress = 0;
        for (int end = 1; end < samples.Count; end++)
        {
            // 没前进的采样只会把站立时间灌进窗里，不做终点
            if (samples[end].Meters <= samples[end - 1].Meters) continue;
            // 站了一会儿再动：新段从这条开始，停留不混进任何窗
            if (samples[end].Elapsed - samples[lastProgress].Elapsed > StaleAfter) start = end;
            lastProgress = end;
            if (start >= end) continue;

            // 距离单调不减，起点随终点单向推进：保持窗内距离刚好 ≥ 窗长
            while (start + 1 < end && samples[end].Meters - samples[start + 1].Meters >= SummaryWindowMeters) start++;

            double meters = samples[end].Meters - samples[start].Meters;
            var span = samples[end].Elapsed - samples[start].Elapsed;
            if (meters < SummaryWindowMeters || span <= TimeSpan.Zero) continue;

            var pace = PerKilometer(span, meters);
            if (pace > MaxMovingPace) continue;
            if (fastest == null || pace < fastest.Value) fastest = pace;
            if (slowest == null || pace > slowest.Value) slowest = pace;
        }
        return new PaceSummary(fastest, slowest);
    }

    private static TimeSpan PerKilometer(TimeSpan span, double meters) => span * (1000 / meters);
}

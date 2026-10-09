namespace Runner.Core.SignalProcessing;

/// <summary>
/// 将同一开机单调时钟上的纳秒采样时间映射到固定 UTC 锚点。
/// 传感器样本与当前时间共用时间基准，不受 GPS UTC 与手机墙钟偏差影响。
/// </summary>
public sealed class SensorClock : TimeProvider
{
    private readonly Func<long> _nanoseconds;
    private readonly long _anchorNanoseconds;
    private readonly DateTimeOffset _anchorTime;

    public SensorClock(Func<long> nanoseconds, DateTimeOffset? startTime = null)
    {
        ArgumentNullException.ThrowIfNull(nanoseconds);
        _nanoseconds = nanoseconds;
        _anchorNanoseconds = nanoseconds();
        _anchorTime = startTime ?? DateTimeOffset.UtcNow;
    }

    public DateTimeOffset GetSampleTime(long timestampNanoseconds) =>
        _anchorTime.AddTicks((timestampNanoseconds - _anchorNanoseconds) / 100);

    public override DateTimeOffset GetUtcNow() => GetSampleTime(_nanoseconds());
    public override long GetTimestamp() => _nanoseconds();
    public override long TimestampFrequency => 1_000_000_000;
}

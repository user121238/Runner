namespace Runner.Core.SignalProcessing;

/// <summary>
/// 加速度兜底计步：合力去重力后平滑，再检测正向脉冲。
/// 滤波系数由实际采样间隔计算；合成信号测试不能替代真机步态校准。
/// </summary>
public sealed class AccelerometerStepDetector
{
    private const double GravityTimeConstantSeconds = 0.5;
    private const double SignalCutoffHz = 5;
    private const long MaxSampleGapMs = 1000;
    private readonly PeakDetector _peaks = new(0.8f, 200);
    private long? _lastTimestamp;
    private double _gravity;
    private double _signal;

    public bool ProcessSample(float x, float y, float z, long timestampMs)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || timestampMs < 0)
            return false;
        if (_lastTimestamp is long last && timestampMs <= last) return false;

        double magnitude = Math.Sqrt((double)x * x + (double)y * y + (double)z * z);
        if (_lastTimestamp is not long previous || timestampMs - previous > MaxSampleGapMs)
        {
            Reset();
            _gravity = magnitude;
            _lastTimestamp = timestampMs;
            return false;
        }

        double dt = (timestampMs - previous) / 1000.0;
        _lastTimestamp = timestampMs;
        _gravity += (1 - Math.Exp(-dt / GravityTimeConstantSeconds)) * (magnitude - _gravity);
        _signal += (1 - Math.Exp(-2 * Math.PI * SignalCutoffHz * dt)) * (magnitude - _gravity - _signal);
        return _peaks.DetectPeak((float)_signal, timestampMs);
    }

    public void Reset()
    {
        _gravity = 0;
        _signal = 0;
        _lastTimestamp = null;
        _peaks.Reset();
    }
}

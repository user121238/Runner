namespace Runner.Core.SignalProcessing;

/// <summary>一次越过阈值再回落的波形只计一个峰，时间戳必须单调递增。</summary>
public class PeakDetector
{
    private readonly float _threshold;
    private readonly long _minIntervalMs;
    private bool _aboveThreshold;
    private long? _lastTimestamp;
    private long? _lastPeakTime;

    public PeakDetector(float threshold, long minIntervalMs)
    {
        if (!float.IsFinite(threshold) || threshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        if (minIntervalMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(minIntervalMs));
        _threshold = threshold;
        _minIntervalMs = minIntervalMs;
    }

    public bool DetectPeak(float value, long timestampMs)
    {
        if (!float.IsFinite(value) || timestampMs < 0) return false;
        if (_lastTimestamp is long last && timestampMs <= last) return false;
        _lastTimestamp = timestampMs;

        if (value > _threshold)
        {
            _aboveThreshold = true;
            return false;
        }

        if (!_aboveThreshold || value >= _threshold) return false;
        _aboveThreshold = false;
        if (_lastPeakTime is long peak && timestampMs - peak < _minIntervalMs) return false;
        _lastPeakTime = timestampMs;
        return true;
    }

    public void Reset()
    {
        _aboveThreshold = false;
        _lastTimestamp = null;
        _lastPeakTime = null;
    }
}

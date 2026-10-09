namespace Runner.Core.SignalProcessing;

/// <summary>用单调时钟的毫秒时间戳统计最近 8 秒的步频。</summary>
public class FrequencyCalculator
{
    private const long WindowMs = 8000;
    private const long InactivityTimeoutMs = 3000;
    private const long MinWindowMs = 1000;
    private readonly Queue<(long Timestamp, long Count)> _samples = new();
    private readonly object _lock = new();
    private long? _lastTimestamp;
    private long? _lastEventTimestamp;
    private long _totalCount;

    public void AddEvent(long timestampMs) => AddEvents(timestampMs, 1);

    /// <summary>批量传感器回调只记录真实计数增量，不伪造每一步的时间戳。</summary>
    public void AddEvents(long timestampMs, int eventCount)
    {
        if (timestampMs < 0 || eventCount < 0) return;
        lock (_lock)
        {
            if (_lastTimestamp is long last && timestampMs <= last) return;
            if (_lastEventTimestamp is long previous && timestampMs - previous >= InactivityTimeoutMs)
            {
                _samples.Clear();
                _lastEventTimestamp = null;
            }

            _totalCount += eventCount;
            _samples.Enqueue((timestampMs, _totalCount));
            _lastTimestamp = timestampMs;
            if (eventCount > 0) _lastEventTimestamp = timestampMs;
            RemoveExpired(timestampMs);
        }
    }

    public double GetFrequency(long currentTimeMs)
    {
        lock (_lock)
        {
            if (_lastTimestamp is not long last || currentTimeMs < last) return 0;
            RemoveExpired(currentTimeMs);
            if (_lastEventTimestamp is not long lastEvent) return 0;
            if (currentTimeMs - lastEvent >= InactivityTimeoutMs)
            {
                _samples.Clear();
                _lastEventTimestamp = null;
                return 0;
            }

            if (_samples.Count < 2) return 0;
            var first = _samples.Peek();
            long duration = last - first.Timestamp;
            return duration < MinWindowMs ? 0 : (_totalCount - first.Count) * 60000.0 / duration;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _samples.Clear();
            _lastTimestamp = null;
            _lastEventTimestamp = null;
            _totalCount = 0;
        }
    }

    private void RemoveExpired(long now)
    {
        while (_samples.Count > 0 && now - _samples.Peek().Timestamp > WindowMs)
            _samples.Dequeue();
    }
}

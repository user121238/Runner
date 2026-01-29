namespace Runner.Core.SignalProcessing
{
    /// <summary>
    /// 频率计算器 - 基于时间窗口计算事件频率
    /// 可用于步频、心率等频率统计
    /// </summary>
    public class FrequencyCalculator
    {
        // 统计窗口， 8 秒
        private const long WINDOW_MS = 8000;
        private const long MIN_WINDOW_MS = 1000; // 最小有意义的时间窗口

        private readonly Queue<long> _eventTimestamps = new();
        private readonly object _lock = new();

        /// <summary>
        /// 记录一次事件
        /// </summary>
        /// <param name="timestampMs">事件的时间戳（毫秒）</param>
        public void AddEvent(long timestampMs)
        {
            lock (_lock)
            {
                _eventTimestamps.Enqueue(timestampMs);
            }
        }

        /// <summary>
        /// 获取当前频率（事件/分钟）
        /// </summary>
        /// <param name="currentTimeMs">当前时间（毫秒）</param>
        public double GetFrequency(long currentTimeMs)
        {
            lock (_lock)
            {
                // 清理窗口外的数据，保留至少2个时间戳
                while (_eventTimestamps.Count > 2 &&
                       currentTimeMs - _eventTimestamps.Peek() > WINDOW_MS)
                {
                    _eventTimestamps.Dequeue();
                }

                if (_eventTimestamps.Count < 2)
                    return 0;

                long oldestEventTime = _eventTimestamps.Peek();
                long actualWindowMs = currentTimeMs - oldestEventTime;

                // 时间窗口太短，返回0
                if (actualWindowMs < MIN_WINDOW_MS)
                    return 0;

                double actualWindowMinutes = actualWindowMs / 60000.0;

                // 频率 = 窗口内事件数 / 实际时间（分钟）
                // 减1是因为计算的是间隔数
                return (_eventTimestamps.Count - 1) / actualWindowMinutes;
            }
        }
    }
}

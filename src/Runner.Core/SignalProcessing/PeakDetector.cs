namespace Runner.Core.SignalProcessing
{
    /// <summary>
    /// 峰值检测器 - 检测信号中的峰值（先上升超过阈值，然后下降）
    /// 可用于步态检测、心率检测、震动检测等场景
    /// </summary>
    public class PeakDetector
    {
        private readonly float _threshold;
        private readonly long _minIntervalMs;

        private bool _isPeakDetected = false;
        private float _lastValue = 0;
        private long _lastPeakTime = 0;

        /// <summary>
        /// 创建峰值检测器
        /// </summary>
        /// <param name="threshold">峰值阈值 - 信号必须超过此值才能进入峰值状态</param>
        /// <param name="minIntervalMs">最小峰值间隔（毫秒）- 防止重复检测</param>
        public PeakDetector(float threshold, long minIntervalMs)
        {
            _threshold = threshold;
            _minIntervalMs = minIntervalMs;
        }

        /// <summary>
        /// 检测峰值
        /// </summary>
        /// <param name="value">当前信号值</param>
        /// <param name="timestampMs">当前时间戳（毫秒）</param>
        /// <returns>如果检测到有效峰值返回 true</returns>
        public bool DetectPeak(float value, long timestampMs)
        {
            bool peakDetected = false;

            // 峰值检测：需要先超过阈值，然后下降
            if (value > _threshold && !_isPeakDetected)
            {
                // 标记进入峰值状态
                _isPeakDetected = true;
            }
            else if (_isPeakDetected && value < _lastValue && value < _threshold)
            {
                // 检测到下降沿，确认为峰值
                long timeSinceLastPeak = timestampMs - _lastPeakTime;

                if (timeSinceLastPeak >= _minIntervalMs)
                {
                    // 确认检测到有效峰值
                    _lastPeakTime = timestampMs;
                    _isPeakDetected = false;
                    peakDetected = true;
                }
                else
                {
                    // 间隔太短，忽略此峰值
                    _isPeakDetected = false;
                }
            }

            _lastValue = value;
            return peakDetected;
        }

        /// <summary>
        /// 重置检测器状态
        /// </summary>
        public void Reset()
        {
            _isPeakDetected = false;
            _lastValue = 0;
            _lastPeakTime = 0;
        }
    }
}

namespace Runner.Core.SignalProcessing
{
    /// <summary>
    /// 低通滤波器 - 用于平滑信号，去除高频噪声
    /// </summary>
    public class LowPassFilter
    {
        private readonly float _alpha;
        private float _filteredValue;

        /// <summary>
        /// 创建低通滤波器
        /// </summary>
        /// <param name="alpha">滤波系数 (0-1)，越小越平滑，越大越接近原始值</param>
        public LowPassFilter(float alpha)
        {
            _alpha = alpha;
        }

        /// <summary>
        /// 对输入值进行滤波
        /// </summary>
        /// <param name="value">原始值</param>
        /// <returns>滤波后的值</returns>
        public float Filter(float value)
        {
            _filteredValue = _alpha * value + (1 - _alpha) * _filteredValue;
            return _filteredValue;
        }

        /// <summary>
        /// 重置滤波器状态
        /// </summary>
        public void Reset()
        {
            _filteredValue = 0;
        }
    }
}

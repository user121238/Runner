using Android.Content;
using Android.Hardware;
using System.Diagnostics;

namespace Runner.Client.Platforms.Android.Native
{
    /// <summary>
    /// 加速度传感器计步实现
    /// 使用峰值检测算法识别步态
    /// </summary>
    public sealed class AccelerometerStepCounter : Java.Lang.Object, IStepCounter, ISensorEventListener
    {
        private SensorManager? _sensorManager;
        private Sensor? _sensor;
        private int _stepCount;
        private int _eventCount = 0;

        // 峰值检测参数
        private const float PEAK_THRESHOLD = 12.0f;
        private const long MIN_STEP_INTERVAL_MS = 200;
        private const long STEP_FREQUENCY_WINDOW_MS = 10000;

        // 低通滤波
        private const float FILTER_ALPHA = 0.1f;
        private float _filteredValue;

        // 峰值检测状态
        private long _lastPeakTime;
        private readonly Queue<long> _stepTimestamps = new();

        public event Action<int>? OnStepCountChanged;
        public event Action<double>? OnStepFrequencyChanged;

        public void Init(Context context)
        {
            Debug.WriteLine("[AccelerometerStepCounter] 开始初始化");

            _sensorManager = (SensorManager?)context.GetSystemService(Context.SensorService);
            _sensor = _sensorManager?.GetDefaultSensor(SensorType.Accelerometer);

            if (_sensor == null)
            {
                Debug.WriteLine("[AccelerometerStepCounter] 错误: 传感器为空");
                throw new InvalidOperationException("设备不支持加速度传感器");
            }

            Debug.WriteLine($"[AccelerometerStepCounter] 初始化成功 - {_sensor.Name}");
        }

        public void Start()
        {
            Debug.WriteLine("[AccelerometerStepCounter] 开始启动传感器");

            if (_sensorManager == null || _sensor == null)
            {
                Debug.WriteLine("[AccelerometerStepCounter] 错误: 未初始化");
                throw new InvalidOperationException("未初始化");
            }

            // 先取消注册，避免重复注册
            _sensorManager.UnregisterListener(this);

            bool registered = _sensorManager.RegisterListener(this, _sensor, SensorDelay.Game);
            Debug.WriteLine($"[AccelerometerStepCounter] 传感器注册结果: {registered}");

            if (!registered)
            {
                Debug.WriteLine("[AccelerometerStepCounter] 错误: 传感器注册失败");
                throw new InvalidOperationException("加速度传感器注册失败");
            }
        }

        public void Stop()
        {
            Debug.WriteLine("[AccelerometerStepCounter] 停止传感器");
            _sensorManager?.UnregisterListener(this);
        }

        public void OnSensorChanged(SensorEvent? e)
        {
            _eventCount++;

            if (e?.Values == null || e.Values.Count < 3)
            {
                if (_eventCount % 100 == 1)
                    Debug.WriteLine($"[AccelerometerStepCounter] 事件#{_eventCount} - 数据不足");
                return;
            }

            // 计算三轴加速度合力
            float x = e.Values[0];
            float y = e.Values[1];
            float z = e.Values[2];
            float magnitude = (float)Math.Sqrt(x * x + y * y + z * z);

            // 低通滤波
            _filteredValue = FILTER_ALPHA * magnitude + (1 - FILTER_ALPHA) * _filteredValue;

            // 每100个事件输出一次调试信息
            if (_eventCount % 100 == 1)
            {
                Debug.WriteLine($"[AccelerometerStepCounter] 事件#{_eventCount} - 原始: {magnitude:F2}, 滤波: {_filteredValue:F2}, 阈值: {PEAK_THRESHOLD}");
            }

            // 峰值检测
            DetectStep(_filteredValue, e.Timestamp);
        }

        private void DetectStep(float value, long timestampNs)
        {
            if (value < PEAK_THRESHOLD)
                return;

            long timestampMs = timestampNs / 1_000_000;
            long timeSinceLastPeak = timestampMs - _lastPeakTime;

            if (timeSinceLastPeak < MIN_STEP_INTERVAL_MS)
                return;

            // 检测到一步
            _stepCount++;
            _lastPeakTime = timestampMs;
            _stepTimestamps.Enqueue(timestampMs);

            Debug.WriteLine($"[AccelerometerStepCounter] 检测到步数 #{_stepCount} - 峰值: {value:F2}, 间隔: {timeSinceLastPeak}ms");

            // 清理过期时间戳
            while (_stepTimestamps.Count > 0 &&
                   timestampMs - _stepTimestamps.Peek() > STEP_FREQUENCY_WINDOW_MS)
            {
                _stepTimestamps.Dequeue();
            }

            // 计算步频
            double windowMinutes = STEP_FREQUENCY_WINDOW_MS / 60000.0;
            double frequency = _stepTimestamps.Count / windowMinutes;

            Debug.WriteLine($"[AccelerometerStepCounter] 步频: {frequency:F1} SPM");

            OnStepCountChanged?.Invoke(_stepCount);
            OnStepFrequencyChanged?.Invoke(frequency);
        }

        public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy)
        {
            Debug.WriteLine($"[AccelerometerStepCounter] 精度变化: {accuracy}");
        }

        public new void Dispose()
        {
            Debug.WriteLine($"[AccelerometerStepCounter] 释放资源 - 共收到 {_eventCount} 个事件, 检测到 {_stepCount} 步");
            Stop();
            _sensorManager = null;
            _sensor = null;
            _stepTimestamps.Clear();
            OnStepCountChanged = null;
            OnStepFrequencyChanged = null;
            base.Dispose();
        }
    }
}

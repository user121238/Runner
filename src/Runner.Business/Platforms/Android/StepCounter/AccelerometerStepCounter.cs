using Android.Content;
using Android.Hardware;
using Android.OS;
using Runner.Core.SignalProcessing;
using Debug = System.Diagnostics.Debug;

namespace Runner.Business.Platforms.Android.StepCounter
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
        private const float PEAK_THRESHOLD = 11.5f;
        private const long MIN_STEP_INTERVAL_MS = 300;
        private const float FILTER_ALPHA = 0.1f;

        // 信号处理
        private readonly LowPassFilter _lowPassFilter;
        private readonly PeakDetector _peakDetector;

        // 步频计算
        private readonly FrequencyCalculator _cadenceCalc = new();

        // 定时器相关
        private System.Threading.Timer? _updateTimer;
        private const int UPDATE_INTERVAL_MS = 1000; // 每1秒更新一次

        public event Action<int>? OnStepCountChanged;
        public event Action<double>? OnStepFrequencyChanged;

        public AccelerometerStepCounter()
        {
            _lowPassFilter = new LowPassFilter(FILTER_ALPHA);
            _peakDetector = new PeakDetector(PEAK_THRESHOLD, MIN_STEP_INTERVAL_MS);
        }

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

            // 启动定时器，每1秒更新一次步频
            _updateTimer = new System.Threading.Timer(OnTimerUpdate, null, UPDATE_INTERVAL_MS, UPDATE_INTERVAL_MS);
            Debug.WriteLine("[AccelerometerStepCounter] 步频更新定时器已启动");
        }

        public void Stop()
        {
            Debug.WriteLine("[AccelerometerStepCounter] 停止传感器");

            // 停止定时器
            _updateTimer?.Dispose();
            _updateTimer = null;

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
            float filteredValue = _lowPassFilter.Filter(magnitude);

            // 每100个事件输出一次调试信息
            if (_eventCount % 100 == 1)
            {
                Debug.WriteLine($"[AccelerometerStepCounter] 事件#{_eventCount} - 原始: {magnitude:F2}, 滤波: {filteredValue:F2}");
            }

            // 峰值检测
            DetectStep(filteredValue, e.Timestamp);
        }

        private void DetectStep(float value, long timestampNs)
        {
            long timestampMs = timestampNs / 1_000_000;

            // 使用峰值检测器
            if (_peakDetector.DetectPeak(value, timestampMs))
            {
                // 确认检测到一步
                _stepCount++;

                _cadenceCalc.AddEvent(timestampMs);

                Debug.WriteLine($"[AccelerometerStepCounter] ✓✓✓ 确认步数 #{_stepCount}");

                // 立即更新步数
                OnStepCountChanged?.Invoke(_stepCount);
            }
        }

        private void OnTimerUpdate(object? state)
        {
            try
            {
                Debug.WriteLine("[AccelerometerStepCounter] 定时器触发");

                long currentTimeMs = SystemClock.ElapsedRealtime();

                // 计算步频
                double frequency = _cadenceCalc.GetFrequency(currentTimeMs);

                Debug.WriteLine($"[AccelerometerStepCounter] 计算得到步频: {frequency:F1} SPM");

                // 触发步频更新事件
                OnStepFrequencyChanged?.Invoke(frequency);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccelerometerStepCounter] 定时器更新异常: {ex}");
            }
        }

        public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy)
        {
            Debug.WriteLine($"[AccelerometerStepCounter] 精度变化: {accuracy}");
        }

        public new void Dispose()
        {
            Debug.WriteLine($"[AccelerometerStepCounter] 释放资源 - 共收到 {_eventCount} 个事件, 检测到 {_stepCount} 步");

            // 停止定时器
            _updateTimer?.Dispose();
            _updateTimer = null;

            Stop();
            _sensorManager = null;
            _sensor = null;
            OnStepCountChanged = null;
            OnStepFrequencyChanged = null;
            base.Dispose();
        }
    }
}

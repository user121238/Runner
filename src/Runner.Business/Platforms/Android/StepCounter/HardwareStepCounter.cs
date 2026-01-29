using Android.Content;
using Android.Hardware;
using Android.OS;
using Runner.Core.SignalProcessing;
using Debug = System.Diagnostics.Debug;

namespace Runner.Business.Platforms.Android.StepCounter
{
    /// <summary>
    /// 硬件计步传感器实现
    /// 同时使用 TYPE_STEP_COUNTER (总步数) 和 TYPE_STEP_DETECTOR (步频)
    /// </summary>
    public sealed class HardwareStepCounter : Java.Lang.Object, IStepCounter, ISensorEventListener
    {
        private SensorManager? _sensorManager;
        private Sensor? _stepCounterSensor;
        private Sensor? _stepDetectorSensor;

        // 步数相关
        private int _totalSteps;
        private int _counterEventCount;

        // 步频相关
        private readonly FrequencyCalculator _cadenceCalc = new();
        private int _detectorEventCount;

        // 定时器相关
        private Timer? _updateTimer;
        private const int UPDATE_INTERVAL_MS = 1000; // 每1秒更新一次

        public event Action<int>? OnStepCountChanged;
        public event Action<double>? OnStepFrequencyChanged;

        public void Init(Context context)
        {
            Debug.WriteLine("[HardwareStepCounter] 开始初始化");

            _sensorManager = (SensorManager?)context.GetSystemService(Context.SensorService);

            // 优先使用步态检测传感器（实时计步和步频）
            _stepDetectorSensor = _sensorManager?.GetDefaultSensor(SensorType.StepDetector);

            // 可选：计步传感器作为备份验证
            _stepCounterSensor = _sensorManager?.GetDefaultSensor(SensorType.StepCounter);

            if (_stepDetectorSensor == null)
            {
                Debug.WriteLine("[HardwareStepCounter] 错误: 步态检测传感器不可用");
                throw new InvalidOperationException("设备不支持步态检测传感器");
            }

            Debug.WriteLine($"[HardwareStepCounter] 找到步态检测传感器: {_stepDetectorSensor.Name}");

            if (_stepCounterSensor != null)
                Debug.WriteLine($"[HardwareStepCounter] 找到计步传感器(备份): {_stepCounterSensor.Name}");
        }

        public void Start()
        {
            Debug.WriteLine("[HardwareStepCounter] 开始启动传感器");

            if (_sensorManager == null || _stepDetectorSensor == null)
            {
                Debug.WriteLine("[HardwareStepCounter] 错误: 未初始化");
                throw new InvalidOperationException("未初始化");
            }

            // 先取消注册
            _sensorManager.UnregisterListener(this);

            // 注册步态检测传感器（主要）
            bool registered = _sensorManager.RegisterListener(this, _stepDetectorSensor, SensorDelay.Normal);
            Debug.WriteLine($"[HardwareStepCounter] 步态检测传感器注册结果: {registered}");

            if (!registered)
            {
                Debug.WriteLine("[HardwareStepCounter] 错误: 传感器注册失败");
                throw new InvalidOperationException("传感器注册失败，可能缺少权限或传感器不可用");
            }

            // 可选：注册计步传感器作为备份验证
            if (_stepCounterSensor != null)
            {
                bool counterRegistered = _sensorManager.RegisterListener(this, _stepCounterSensor, SensorDelay.Normal);
                Debug.WriteLine($"[HardwareStepCounter] 计步传感器(备份)注册结果: {counterRegistered}");
            }

            // 启动定时器，每1秒更新一次步频
            _updateTimer = new Timer(OnTimerUpdate, null, UPDATE_INTERVAL_MS, UPDATE_INTERVAL_MS);
            Debug.WriteLine("[HardwareStepCounter] 步频更新定时器已启动");
        }

        public void Stop()
        {
            Debug.WriteLine("[HardwareStepCounter] 停止传感器");

            // 停止定时器
            _updateTimer?.Dispose();
            _updateTimer = null;

            _sensorManager?.UnregisterListener(this);
        }


        public void OnSensorChanged(SensorEvent? e)
        {
            if (e?.Sensor == null || e.Values == null)
                return;

            // 根据传感器类型分别处理
            if (e.Sensor.Type == SensorType.StepCounter)
            {
                HandleStepCounter(e);

            }
            else if (e.Sensor.Type == SensorType.StepDetector)
            {
                HandleStepDetector(e);
            }
        }

        private void HandleStepCounter(SensorEvent e)
        {
            _counterEventCount++;

            if (e.Values!.Count == 0)
                return;

            int hardwareSteps = (int)e.Values[0];

            // 仅用于日志对比验证
            Debug.WriteLine($"[HardwareStepCounter] 计步传感器报告: {hardwareSteps} (检测器计数: {_totalSteps})");
        }

        private void HandleStepDetector(SensorEvent e)
        {
            _detectorEventCount++;
            _totalSteps++;

            long timestampMs = e.Timestamp / 1_000_000;

            _cadenceCalc.AddEvent(timestampMs);

            Debug.WriteLine($"[HardwareStepCounter] 检测到第 {_totalSteps} 步 - 时间戳: {timestampMs}");

            // 立即更新步数
            OnStepCountChanged?.Invoke(_totalSteps);

            // 步频由定时器更新，这里不更新
        }

        private void OnTimerUpdate(object? state)
        {
            try
            {
                Debug.WriteLine("[HardwareStepCounter] 定时器触发");

                long currentTimeMs = SystemClock.ElapsedRealtime();
                Debug.WriteLine($"[HardwareStepCounter] 当前时间: {currentTimeMs}");

                // 计算步频
                double frequency = _cadenceCalc.GetFrequency(currentTimeMs);

                Debug.WriteLine($"[HardwareStepCounter] 计算得到步频: {frequency:F1} SPM");

                // 触发步频更新事件
                OnStepFrequencyChanged?.Invoke(frequency);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareStepCounter] 定时器更新异常: {ex}");
            }
        }


        public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy)
        {
            Debug.WriteLine($"[HardwareStepCounter] 精度变化: {sensor?.Type} - {accuracy}");
        }

        public new void Dispose()
        {
            Debug.WriteLine($"[HardwareStepCounter] 释放资源 - 计步事件: {_counterEventCount}, 步态检测事件: {_detectorEventCount}");

            // 停止定时器
            _updateTimer?.Dispose();
            _updateTimer = null;

            Stop();
            _sensorManager = null;
            _stepCounterSensor = null;
            _stepDetectorSensor = null;
            OnStepCountChanged = null;
            OnStepFrequencyChanged = null;
            base.Dispose();
        }
    }
}

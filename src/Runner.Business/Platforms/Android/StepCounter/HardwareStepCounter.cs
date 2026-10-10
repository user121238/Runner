using Android.Content;
using Android.Hardware;
using Android.OS;
using Runner.Business.StepCounter;
using Runner.Core.SignalProcessing;
using Debug = System.Diagnostics.Debug;

namespace Runner.Business.Platforms.Android.StepCounter;

public sealed class HardwareStepCounter : Java.Lang.Object, IStepCounter, ISensorEventListener
{
    private SensorManager? _sensorManager;
    private Sensor? _stepCounterSensor;
    private Sensor? _stepDetectorSensor;
    private readonly object _sync = new();
    private readonly StepCountAccumulator _steps = new();
    private readonly StepCountAccumulator _counterSteps = new();
    private readonly FrequencyCalculator _cadence = new();
    private Timer? _timer;
    private bool _running;
    private bool _usingDetector;
    private bool _usingCounter;

    public event Action<int>? OnStepCountChanged;
    public event Action<double>? OnStepFrequencyChanged;
    public event Action<string>? OnError;

    public void Init(Context context)
    {
        _sensorManager = (SensorManager?)context.GetSystemService(Context.SensorService);
        _stepDetectorSensor = _sensorManager?.GetDefaultSensor(SensorType.StepDetector, true)
            ?? _sensorManager?.GetDefaultSensor(SensorType.StepDetector);
        _stepCounterSensor = _sensorManager?.GetDefaultSensor(SensorType.StepCounter, true)
            ?? _sensorManager?.GetDefaultSensor(SensorType.StepCounter);
        if (_stepDetectorSensor == null && _stepCounterSensor == null)
            throw new InvalidOperationException("设备不支持硬件计步传感器");
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_running) return;
            if (_sensorManager == null) throw new InvalidOperationException("计步器未初始化");
            _steps.BeginSegment();
            _counterSteps.BeginSegment();
            _cadence.Reset();
            _usingDetector = _stepDetectorSensor != null &&
                _sensorManager.RegisterListener(this, _stepDetectorSensor, SensorDelay.Normal);
            _usingCounter = _stepCounterSensor != null &&
                _sensorManager.RegisterListener(this, _stepCounterSensor, SensorDelay.Normal);
            if (!_usingDetector && !_usingCounter)
                throw new InvalidOperationException("硬件计步传感器注册失败");
            _running = true;
            _timer = new Timer(UpdateCadence, null, 1000, 1000);
            Debug.WriteLine($"[Runner.Step] 硬件模式: count={(_usingDetector ? _stepDetectorSensor?.Name : _stepCounterSensor?.Name)}; counterCrossCheck={_usingCounter}");
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _running = false;
            _timer?.Dispose();
            _timer = null;
            _sensorManager?.UnregisterListener(this);
            _steps.BeginSegment();
            _counterSteps.BeginSegment();
            _cadence.Reset();
            OnStepFrequencyChanged?.Invoke(0);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _steps.Reset();
            _counterSteps.Reset();
            _cadence.Reset();
            OnStepCountChanged?.Invoke(0);
            OnStepFrequencyChanged?.Invoke(0);
        }
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        try { ProcessSensorEvent(e); }
        catch (Exception ex) { OnError?.Invoke($"处理硬件计步数据失败: {ex.Message}"); }
    }

    private void ProcessSensorEvent(SensorEvent? e)
    {
        if (e?.Sensor == null || e.Values == null || e.Values.Count == 0) return;
        lock (_sync)
        {
            if (!_running) return;
            long time = e.Timestamp / 1_000_000;
            if (_usingDetector && e.Sensor.Type == SensorType.StepDetector)
            {
                _steps.AddStep();
                _cadence.AddEvent(time);
                Debug.WriteLine($"[Runner.Step] detector event ageMs={SystemClock.ElapsedRealtime() - time}");
            }
            else if (_usingCounter && e.Sensor.Type == SensorType.StepCounter)
            {
                int before = _counterSteps.Count;
                _counterSteps.UpdateHardwareTotal((int)e.Values[0]);
                if (!_usingDetector) _cadence.AddEvents(time, _counterSteps.Count - before);
                Debug.WriteLine($"[Runner.Step] counter delta={_counterSteps.Count - before} total={_counterSteps.Count} ageMs={SystemClock.ElapsedRealtime() - time}");
            }
            else return;
            OnStepCountChanged?.Invoke(CurrentCount);
        }
    }

    private void UpdateCadence(object? state)
    {
        lock (_sync)
        {
            if (!_running) return;
            double cadence = _cadence.GetFrequency(SystemClock.ElapsedRealtime());
            Debug.WriteLine($"[Runner.Step] hardware count={CurrentCount} cadence={cadence:F1}");
            OnStepFrequencyChanged?.Invoke(cadence);
        }
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }

    private int CurrentCount => _usingDetector ? _steps.Count : _counterSteps.Count;

    public new void Dispose()
    {
        Stop();
        _sensorManager = null;
        _stepCounterSensor = null;
        _stepDetectorSensor = null;
        OnStepCountChanged = null;
        OnStepFrequencyChanged = null;
        OnError = null;
        base.Dispose();
    }
}

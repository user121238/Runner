using Android.Content;
using Android.Hardware;
using Android.OS;
using Runner.Core.SignalProcessing;
using Debug = System.Diagnostics.Debug;

namespace Runner.Business.Platforms.Android.StepCounter;

public sealed class AccelerometerStepCounter : Java.Lang.Object, IStepCounter, ISensorEventListener
{
    private SensorManager? _sensorManager;
    private Sensor? _sensor;
    private readonly object _sync = new();
    private readonly AccelerometerStepDetector _detector = new();
    private readonly StepCountAccumulator _steps = new();
    private readonly FrequencyCalculator _cadence = new();
    private Timer? _timer;
    private bool _running;

    public event Action<int>? OnStepCountChanged;
    public event Action<double>? OnStepFrequencyChanged;

    public void Init(Context context)
    {
        _sensorManager = (SensorManager?)context.GetSystemService(Context.SensorService);
        _sensor = _sensorManager?.GetDefaultSensor(SensorType.Accelerometer)
            ?? throw new InvalidOperationException("设备不支持加速度传感器");
        Debug.WriteLine($"[Runner.Step] 加速度传感器: {_sensor.Name}");
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_running) return;
            if (_sensorManager == null || _sensor == null)
                throw new InvalidOperationException("计步器未初始化");
            _detector.Reset();
            _cadence.Reset();
            if (!_sensorManager.RegisterListener(this, _sensor, SensorDelay.Game))
                throw new InvalidOperationException("加速度传感器注册失败");
            _running = true;
            _timer = new Timer(UpdateCadence, null, 1000, 1000);
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
            _detector.Reset();
            _cadence.Reset();
            OnStepFrequencyChanged?.Invoke(0);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _steps.Reset();
            _detector.Reset();
            _cadence.Reset();
            OnStepCountChanged?.Invoke(0);
            OnStepFrequencyChanged?.Invoke(0);
        }
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values == null || e.Values.Count < 3) return;
        lock (_sync)
        {
            if (!_running) return;
            long time = e.Timestamp / 1_000_000;
            if (!_detector.ProcessSample(e.Values[0], e.Values[1], e.Values[2], time)) return;
            _steps.AddStep();
            _cadence.AddEvent(time);
            OnStepCountChanged?.Invoke(_steps.Count);
        }
    }

    private void UpdateCadence(object? state)
    {
        lock (_sync)
        {
            if (!_running) return;
            double cadence = _cadence.GetFrequency(SystemClock.ElapsedRealtime());
            Debug.WriteLine($"[Runner.Step] accelerometer count={_steps.Count} cadence={cadence:F1}");
            OnStepFrequencyChanged?.Invoke(cadence);
        }
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }

    public new void Dispose()
    {
        Stop();
        _sensorManager = null;
        _sensor = null;
        OnStepCountChanged = null;
        OnStepFrequencyChanged = null;
        base.Dispose();
    }
}

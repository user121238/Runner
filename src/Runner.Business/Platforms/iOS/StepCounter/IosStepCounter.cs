using CoreMotion;
using Foundation;
using Runner.Business.StepCounter;

namespace Runner.Business.Platforms.iOS.StepCounter;

/// <summary>CMPedometer 每次 Start 从新的 NSDate 累计；只把该段累计值加一次，暂停时间不计步。</summary>
public sealed class IosStepCounter(TimeProvider clock) : IStepCounter
{
    private readonly CMPedometer _pedometer = new();
    private Timer? _timer;
    private int _generation;
    private int _totalSteps;
    private int _stepsBeforeSegment;
    private int _lastSegmentSteps;
    private double _lastSampleTime;
    private long _lastCadenceTimestamp;
    private bool _running;
    private bool _disposed;

    public event Action<int>? OnStepCountChanged;
    public event Action<double>? OnStepFrequencyChanged;
    public event Action<string>? OnError;

    public static async Task<bool> RequestPermissionAsync()
    {
        if (CMPedometer.AuthorizationStatus != CMAuthorizationStatus.NotDetermined)
            return CMPedometer.AuthorizationStatus == CMAuthorizationStatus.Authorized;

        using var pedometer = new CMPedometer();
        using var start = NSDate.FromTimeIntervalSinceNow(-1);
        using var end = NSDate.Now;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // 首次查询会触发系统“运动与健身”授权，不需要先开始一次伪运动。
        pedometer.QueryPedometerData(start, end, (_, _) =>
            completion.TrySetResult(CMPedometer.AuthorizationStatus == CMAuthorizationStatus.Authorized));
        return await completion.Task;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) return;
        if (CMPedometer.AuthorizationStatus != CMAuthorizationStatus.Authorized)
            throw new InvalidOperationException("需要运动与健身权限");

        using var start = NSDate.Now;
        _stepsBeforeSegment = _totalSteps;
        _lastSegmentSteps = 0;
        _lastSampleTime = start.SecondsSince1970;
        _lastCadenceTimestamp = clock.GetTimestamp();
        _running = true;
        int generation = ++_generation;
        _pedometer.StartPedometerUpdates(start, (data, error) => OnNativeUpdate(generation, data, error));
        _timer = new Timer(_ => MainThread.BeginInvokeOnMainThread(() => ExpireCadence(generation)), null, 1000, 1000);
    }

    private void OnNativeUpdate(int generation, CMPedometerData? data, NSError? error)
    {
        // CoreMotion 在自己的队列回调；只把值传回主线程，并在执行时检查代次。
        string? errorText = error?.LocalizedDescription;
        bool hasData = data != null;
        int steps = data?.NumberOfSteps.Int32Value ?? 0;
        double endTime = data?.EndDate.SecondsSince1970 ?? 0;
        double? cadence = data?.CurrentCadence?.DoubleValue;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_running || generation != _generation) return;
            if (errorText != null)
            {
                OnError?.Invoke($"计步失败: {errorText}");
                return;
            }
            if (!hasData || steps < _lastSegmentSteps) return;
            int increment = steps - _lastSegmentSteps;
            double seconds = endTime - _lastSampleTime;
            double actualCadence = cadence is >= 0 ? cadence.Value * 60
                : seconds > 0 ? increment / seconds * 60 : 0;
            _totalSteps = checked(_stepsBeforeSegment + steps);
            _lastSegmentSteps = steps;
            _lastSampleTime = endTime;
            _lastCadenceTimestamp = clock.GetTimestamp();
            OnStepCountChanged?.Invoke(_totalSteps);
            OnStepFrequencyChanged?.Invoke(double.IsFinite(actualCadence) ? actualCadence : 0);
        });
    }

    private void ExpireCadence(int generation)
    {
        if (_running && generation == _generation && clock.GetElapsedTime(_lastCadenceTimestamp).TotalSeconds >= 3)
            OnStepFrequencyChanged?.Invoke(0);
    }

    public void Stop()
    {
        _running = false;
        ++_generation;
        _timer?.Dispose();
        _timer = null;
        _pedometer.StopPedometerUpdates();
        OnStepFrequencyChanged?.Invoke(0);
    }

    public void Reset()
    {
        bool restart = _running;
        if (restart) Stop();
        _totalSteps = 0;
        _stepsBeforeSegment = 0;
        _lastSegmentSteps = 0;
        OnStepCountChanged?.Invoke(0);
        OnStepFrequencyChanged?.Invoke(0);
        if (restart) Start();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        _pedometer.Dispose();
        OnStepCountChanged = null;
        OnStepFrequencyChanged = null;
        OnError = null;
    }
}

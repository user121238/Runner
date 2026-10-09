using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Runner.Business.Location;
using Runner.Business.Platforms.Android.Media;
using Runner.Business.Platforms.Android.Session;
using Runner.Business.Platforms.Android.StepCounter;

namespace Runner.Client.ViewModels;

/// <summary>诊断页：逐个来源单独起停，真机排障用。来源实例来自 RunSessionHost，不自建。</summary>
public class DiagnosticsPageViewModel : BindableBase, IDestructible
{
    private readonly RunSessionHost _host;
    private IStepCounter? _stepCounter;
    private ILocationTracker? _locationTracker;
    private bool _isPedometerActive;

    private int _stepCount;
    private double _stepFrequency;
    private string _pedometerStatus = "";
    private double _gpsDistance;
    private double _gpsSpeed;
    private string _gpsStatus = "";

    public DiagnosticsPageViewModel(RunSessionHost host)
    {
        _host = host;

        NextTrackCommand = new DelegateCommand(() => MediaController.NextTrack(Platform.AppContext));
        PreviousTrackCommand = new DelegateCommand(() => MediaController.PreviousTrack(Platform.AppContext));
        PlayPauseCommand = new DelegateCommand(() => MediaController.PlayPause(Platform.AppContext));
        StartTickCommand = new DelegateCommand(() => _host.Metronome.Start(_host.Settings.Current.TargetCadence));
        StopTickCommand = new DelegateCommand(_host.Metronome.Stop);
        StartPedometerCommand = new DelegateCommand(StartPedometer);
        StopPedometerCommand = new DelegateCommand(StopPedometer);
        ResetStepCountCommand = new DelegateCommand(ResetStepCount);
        SwitchToAccelerometerCommand = new AsyncDelegateCommand(() => SwitchStepCounterAsync(forceAccelerometer: true));
        SwitchToHardwareCommand = new AsyncDelegateCommand(() => SwitchStepCounterAsync(forceAccelerometer: false));
        StartGpsTrackingCommand = new AsyncDelegateCommand(StartGpsTrackingAsync);
        StopGpsTrackingCommand = new DelegateCommand(StopGpsTracking);
        ResetGpsDistanceCommand = new DelegateCommand(ResetGpsDistance);

        BindSources();
    }

    public DelegateCommand NextTrackCommand { get; }
    public DelegateCommand PreviousTrackCommand { get; }
    public DelegateCommand PlayPauseCommand { get; }
    public DelegateCommand StartTickCommand { get; }
    public DelegateCommand StopTickCommand { get; }
    public DelegateCommand StartPedometerCommand { get; }
    public DelegateCommand StopPedometerCommand { get; }
    public DelegateCommand ResetStepCountCommand { get; }
    public AsyncDelegateCommand SwitchToAccelerometerCommand { get; }
    public AsyncDelegateCommand SwitchToHardwareCommand { get; }
    public AsyncDelegateCommand StartGpsTrackingCommand { get; }
    public DelegateCommand StopGpsTrackingCommand { get; }
    public DelegateCommand ResetGpsDistanceCommand { get; }

    public int StepCount { get => _stepCount; private set => SetProperty(ref _stepCount, value); }
    public double StepFrequency { get => _stepFrequency; private set => SetProperty(ref _stepFrequency, value); }
    public string PedometerStatus { get => _pedometerStatus; private set => SetProperty(ref _pedometerStatus, value); }
    public double GpsDistance { get => _gpsDistance; private set => SetProperty(ref _gpsDistance, value); }
    public double GpsSpeed { get => _gpsSpeed; private set => SetProperty(ref _gpsSpeed, value); }
    public string GpsStatus { get => _gpsStatus; private set => SetProperty(ref _gpsStatus, value); }

    private void BindSources()
    {
        UnbindSources();
        _stepCounter = _host.StepCounter;
        _locationTracker = _host.LocationTracker;
        if (_stepCounter != null)
        {
            _stepCounter.OnStepCountChanged += OnStepCountChanged;
            _stepCounter.OnStepFrequencyChanged += OnStepFrequencyChanged;
        }
        if (_locationTracker != null)
        {
            _locationTracker.OnDistanceUpdated += OnGpsDistanceUpdated;
            _locationTracker.OnSpeedUpdated += OnGpsSpeedUpdated;
            _locationTracker.OnError += OnGpsError;
            _locationTracker.OnStatusUpdated += OnGpsStatusUpdated;
        }
        PedometerStatus = _host.StepCounterStatus;
        GpsStatus = _host.LocationStatus;
    }

    private void UnbindSources()
    {
        if (_stepCounter != null)
        {
            _stepCounter.OnStepCountChanged -= OnStepCountChanged;
            _stepCounter.OnStepFrequencyChanged -= OnStepFrequencyChanged;
            _stepCounter = null;
        }
        if (_locationTracker != null)
        {
            _locationTracker.OnDistanceUpdated -= OnGpsDistanceUpdated;
            _locationTracker.OnSpeedUpdated -= OnGpsSpeedUpdated;
            _locationTracker.OnError -= OnGpsError;
            _locationTracker.OnStatusUpdated -= OnGpsStatusUpdated;
            _locationTracker = null;
        }
    }

    public void Destroy() => UnbindSources();

    #region 计步器

    private void OnStepCountChanged(int steps) => MainThread.BeginInvokeOnMainThread(() => StepCount = steps);

    private void OnStepFrequencyChanged(double spm) => MainThread.BeginInvokeOnMainThread(() => StepFrequency = spm);

    private void StartPedometer()
    {
        if (_stepCounter == null || _isPedometerActive) return;
        try
        {
            _stepCounter.Start();
            _isPedometerActive = true;
            PedometerStatus = _stepCounter is HardwareStepCounter
                ? "运行中（硬件可能先确认多步再上报）"
                : "运行中（加速度兜底，精度需实测）";
        }
        catch (Exception ex)
        {
            PedometerStatus = $"启动失败: {ex.Message}";
        }
    }

    private void StopPedometer()
    {
        if (_stepCounter == null || !_isPedometerActive) return;
        try
        {
            _stepCounter.Stop();
            _isPedometerActive = false;
            PedometerStatus = _host.StepCounterStatus;
        }
        catch (Exception ex)
        {
            PedometerStatus = $"停止失败: {ex.Message}";
        }
    }

    private void ResetStepCount()
    {
        _stepCounter?.Reset();
        StepCount = 0;
        StepFrequency = 0;
    }

    private async Task SwitchStepCounterAsync(bool forceAccelerometer)
    {
        if (_isPedometerActive)
        {
            _stepCounter?.Stop();
            _isPedometerActive = false;
        }

        if (!await _host.SwitchStepCounterAsync(forceAccelerometer))
        {
            PedometerStatus = "会话进行中，不能切换传感器";
            return;
        }

        StepCount = 0;
        StepFrequency = 0;
        BindSources();
    }

    #endregion

    #region GPS

    private void OnGpsDistanceUpdated(double meters) => MainThread.BeginInvokeOnMainThread(() => GpsDistance = meters / 1000.0);

    private void OnGpsSpeedUpdated(double kmh) => MainThread.BeginInvokeOnMainThread(() => GpsSpeed = kmh);

    private void OnGpsError(string error) => MainThread.BeginInvokeOnMainThread(() => GpsStatus = $"错误: {error}");

    private void OnGpsStatusUpdated(string status) => MainThread.BeginInvokeOnMainThread(() => GpsStatus = status);

    private async Task StartGpsTrackingAsync()
    {
        if (_locationTracker == null)
        {
            GpsStatus = _host.LocationStatus;
            return;
        }
        if (_locationTracker.IsTracking) return;
        try
        {
            await _locationTracker.StartAsync();
        }
        catch (Exception ex)
        {
            GpsStatus = $"启动失败: {ex.Message}";
        }
    }

    private void StopGpsTracking()
    {
        if (_locationTracker == null || !_locationTracker.IsTracking) return;
        try
        {
            _locationTracker.Stop();
        }
        catch (Exception ex)
        {
            GpsStatus = $"停止失败: {ex.Message}";
        }
    }

    private void ResetGpsDistance()
    {
        _locationTracker?.Reset();
        GpsDistance = 0;
        GpsSpeed = 0;
    }

    #endregion
}

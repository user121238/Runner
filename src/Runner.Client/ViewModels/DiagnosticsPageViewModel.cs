using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Runner.Business.Location;
using Runner.Business.Media;
using Runner.Business.Session;
using Runner.Business.StepCounter;
#if IOS
using Runner.Business.Platforms.iOS.Media;
#endif

namespace Runner.Client.ViewModels;

/// <summary>诊断页：逐个来源单独起停，真机排障用。来源实例来自 RunSessionHost，不自建；进入时初始化 host（申请权限、创建来源）。</summary>
public class DiagnosticsPageViewModel : BindableBase, INavigatedAware, IDestructible
{
    private const int MetronomeCadence = 180;
    private readonly RunSessionHost _host;
    private IStepCounter? _stepCounter;
    private ILocationTracker? _locationTracker;
    private bool _isPedometerActive;
    private bool? _lastCanModifySources;

    private int _stepCount;
    private double _stepFrequency;
    private string _pedometerStatus = "";
    private double _gpsDistance;
    private double _gpsSpeed;
    private string _gpsStatus = "";
    private string _mediaRemoteStatus = "";
    private string _shortcutsStatus = "";

    public DiagnosticsPageViewModel(RunSessionHost host)
    {
        _host = host;

        NextTrackCommand = new DelegateCommand(() => RunDiagnostic(_host.NextTrack), CanUseMusic);
        PreviousTrackCommand = new DelegateCommand(() => RunDiagnostic(_host.PreviousTrack), CanUseMusic);
        PlayPauseCommand = new DelegateCommand(() => RunDiagnostic(_host.PlayPause), CanUseMusic);
        MediaRemotePreviousCommand = new DelegateCommand(() => RunMediaRemote("上一首", MediaRemoteCommand.PreviousTrack));
        MediaRemoteNextCommand = new DelegateCommand(() => RunMediaRemote("下一首", MediaRemoteCommand.NextTrack));
        MediaRemotePauseCommand = new DelegateCommand(() => RunMediaRemote("暂停", MediaRemoteCommand.Pause));
        MediaRemotePlayCommand = new DelegateCommand(() => RunMediaRemote("播放", MediaRemoteCommand.Play));
        ShortcutsPreviousCommand = new AsyncDelegateCommand(() => RunShortcutAsync("上一首"));
        ShortcutsNextCommand = new AsyncDelegateCommand(() => RunShortcutAsync("下一首"));
        ShortcutsPauseCommand = new AsyncDelegateCommand(() => RunShortcutAsync("暂停"));
        ShortcutsPlayCommand = new AsyncDelegateCommand(() => RunShortcutAsync("播放"));
        StartTickCommand = new DelegateCommand(() => RunDiagnostic(() => _host.Metronome.Start(MetronomeCadence)), CanModify);
        StopTickCommand = new DelegateCommand(() => RunDiagnostic(_host.Metronome.Stop), CanModify);
        StartPedometerCommand = new DelegateCommand(() => RunDiagnostic(StartPedometer), CanModify);
        StopPedometerCommand = new DelegateCommand(() => RunDiagnostic(StopPedometer), CanModify);
        ResetStepCountCommand = new DelegateCommand(() => RunDiagnostic(ResetStepCount), CanModify);
        SwitchToAccelerometerCommand = new AsyncDelegateCommand(() => SwitchStepCounterAsync(forceAccelerometer: true), CanSwitchSource);
        SwitchToHardwareCommand = new AsyncDelegateCommand(() => SwitchStepCounterAsync(forceAccelerometer: false), CanSwitchSource);
        StartGpsTrackingCommand = new AsyncDelegateCommand(StartGpsTrackingAsync, CanModify);
        StopGpsTrackingCommand = new DelegateCommand(() => RunDiagnostic(StopGpsTracking), CanModify);
        ResetGpsDistanceCommand = new DelegateCommand(() => RunDiagnostic(ResetGpsDistance), CanModify);
        _host.Updated += OnHostUpdated;
        RefreshAvailability();
    }

    public DelegateCommand NextTrackCommand { get; }
    public DelegateCommand PreviousTrackCommand { get; }
    public DelegateCommand PlayPauseCommand { get; }
    public DelegateCommand MediaRemotePreviousCommand { get; }
    public DelegateCommand MediaRemoteNextCommand { get; }
    public DelegateCommand MediaRemotePauseCommand { get; }
    public DelegateCommand MediaRemotePlayCommand { get; }
    public AsyncDelegateCommand ShortcutsPreviousCommand { get; }
    public AsyncDelegateCommand ShortcutsNextCommand { get; }
    public AsyncDelegateCommand ShortcutsPauseCommand { get; }
    public AsyncDelegateCommand ShortcutsPlayCommand { get; }
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
    public bool CanControlMusic => _host.CanControlMusic;
    public bool CanSwitchStepCounter => _host.CanSwitchStepCounter;
    public string MusicControlStatus => _host.MusicControlStatus;
    public string MediaRemoteStatus { get => _mediaRemoteStatus; private set => SetProperty(ref _mediaRemoteStatus, value); }
    public string ShortcutsStatus { get => _shortcutsStatus; private set => SetProperty(ref _shortcutsStatus, value); }
#if IOS
    public bool CanTestIosMedia => true;
#else
    public bool CanTestIosMedia => false;
#endif
    public string DiagnosticNotice => _host.IsSessionActive
        ? "运动进行中：诊断页仅查看数据，结束运动后再操作来源。"
        : "手动操作只用于排障，开始运动后由会话统一管理来源。";

    private bool CanModify() => _host.IsInitialized && !_host.IsSessionActive;
    private bool CanUseMusic() => CanModify() && _host.CanControlMusic;
    private bool CanSwitchSource() => CanModify() && _host.CanSwitchStepCounter;

    private void RunDiagnostic(Action action)
    {
        if (!CanModify()) return;
        try { action(); }
        catch (Exception ex) { GpsStatus = $"诊断操作失败: {ex.Message}"; }
    }

    private void OnHostUpdated() => MainThread.BeginInvokeOnMainThread(RefreshAvailability);

    private void RefreshAvailability()
    {
        bool canModify = CanModify();
        if (_lastCanModifySources == canModify) return;
        _lastCanModifySources = canModify;
        RaisePropertyChanged(nameof(DiagnosticNotice));
        NextTrackCommand.RaiseCanExecuteChanged();
        PreviousTrackCommand.RaiseCanExecuteChanged();
        PlayPauseCommand.RaiseCanExecuteChanged();
        StartTickCommand.RaiseCanExecuteChanged();
        StopTickCommand.RaiseCanExecuteChanged();
        StartPedometerCommand.RaiseCanExecuteChanged();
        StopPedometerCommand.RaiseCanExecuteChanged();
        ResetStepCountCommand.RaiseCanExecuteChanged();
        SwitchToAccelerometerCommand.RaiseCanExecuteChanged();
        SwitchToHardwareCommand.RaiseCanExecuteChanged();
        StartGpsTrackingCommand.RaiseCanExecuteChanged();
        StopGpsTrackingCommand.RaiseCanExecuteChanged();
        ResetGpsDistanceCommand.RaiseCanExecuteChanged();
    }

    public void OnNavigatedTo(INavigationParameters parameters) => _ = InitializeAsync();

    public void OnNavigatedFrom(INavigationParameters parameters) { }

    private async Task InitializeAsync()
    {
        try
        {
            await _host.InitializeAsync();
        }
        catch (Exception ex)
        {
            GpsStatus = $"初始化失败: {ex.Message}";
        }
        BindSources();
        RefreshAvailability();
#if IOS
        MediaRemoteStatus = MediaRemoteController.TryLoad()
            ? "MediaRemote 已加载，可直接测试"
            : $"MediaRemote 加载失败: {MediaRemoteController.LoadError}";
#endif
    }

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

    public void Destroy()
    {
        _host.Updated -= OnHostUpdated;
        UnbindSources();
    }

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
            PedometerStatus = $"运行中（{_host.StepCounterStatus}）";
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
        if (!CanSwitchSource()) return;
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
        if (!CanModify()) return;
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

    #region iOS 媒体控制测试

    private void RunMediaRemote(string action, MediaRemoteCommand command)
    {
#if IOS
        try
        {
            MediaRemoteController.Send(command);
            MediaRemoteStatus = $"{action}: 命令已发送，听声音确认";
        }
        catch (Exception ex)
        {
            MediaRemoteStatus = $"{action}: 失败 - {ex.Message}";
        }
#else
        MediaRemoteStatus = "仅 iOS 支持";
#endif
    }

    private Task RunShortcutAsync(string name)
    {
#if IOS
        return RunShortcutCoreAsync(name);
#else
        ShortcutsStatus = "仅 iOS 支持";
        return Task.CompletedTask;
#endif
    }

#if IOS
    private async Task RunShortcutCoreAsync(string name)
    {
        try
        {
            ShortcutsStatus = await ShortcutsController.RunAsync(name)
                ? $"已请求运行「{name}」"
                : $"无法打开「{name}」，请先在快捷指令 App 创建";
        }
        catch (Exception ex)
        {
            ShortcutsStatus = $"运行「{name}」失败: {ex.Message}";
        }
    }
#endif

    #endregion
}

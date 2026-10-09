using _Microsoft.Android.Resource.Designer;
using CommunityToolkit.Mvvm.Input;
using DM.Maui.Framework.Abstractions.Navigation;
using DM.Maui.Framework.Core.Mvvm;
using Microsoft.Extensions.Logging;
using Runner.Business.Platforms.Android.StepCounter;
using Runner.Business.Platforms.Android.Media;
using Runner.Business.Platforms.Android.Permissions;
using Runner.Business.Platforms.Android.Location;
using Runner.Business.Platforms.Android.Session;
using Runner.Business.Location;
using Runner.Core.Session;

namespace Runner.Client.ViewModels;

/// <summary>
/// 主页 ViewModel - 运动会话，以及各来源的诊断控制
/// </summary>
public partial class MainViewModel : BaseViewModel
{
    private const int DefaultTargetCadence = 180;

    private readonly CadencePlay _cadencePlay;
    private IStepCounter? _stepCounter;
    private bool _isPedometerActive = false;
    private ILocationTracker? _locationTracker;
    private RunSessionController? _sessionController;

    // 计步器相关属性
    private int _stepCount = 0;
    private double _stepFrequency = 0.0;
    private string _pedometerStatus = "未启动";

    // GPS 相关属性
    private double _gpsDistance = 0.0;
    private double _gpsSpeed = 0.0;
    private string _gpsStatus = "未启动";

    // 运动会话属性
    private string _sessionStatus = "等待权限";
    private string _sessionMessage = "";
    private string _sessionElapsed = "00:00:00";
    private double _sessionDistanceKm = 0.0;
    private double _sessionCadence = 0.0;
    private int _sessionTargetCadence = DefaultTargetCadence;
    private string _sessionSummary = "";
    private bool _cadenceAvailable = true;

    public MainViewModel(ILogger<MainViewModel> logger, INavigationService navigation) : base(logger, navigation)
    {
        _cadencePlay = new CadencePlay();
        // 延迟初始化音频资源，避免阻塞UI线程

        Task.Run(() => _cadencePlay.Init(ResourceConstant.Raw.beep3));

        // 权限请求串行执行，避免首次安装时多个权限对话框互相打断。
        MainThread.BeginInvokeOnMainThread(async () => await InitializeTrackingAsync());
    }

    private async Task InitializeTrackingAsync()
    {
        await RequestActivityRecognitionPermission();
        await RequestLocationPermission();

        // 前台服务的通知需要此权限；服务本身随运动会话起停。
        try
        {
            await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "请求通知权限失败");
        }

        RebuildSessionController();
    }

    private async Task RequestActivityRecognitionPermission()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 检查活动识别权限");

            var status = await Permissions.CheckStatusAsync<ActivityRecognitionPermission>();
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 当前权限状态: {status}");

            if (status != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 请求活动识别权限");
                status = await Permissions.RequestAsync<ActivityRecognitionPermission>();
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 权限请求结果: {status}");
            }

            if (status == PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 权限已授予，创建计步器");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CreateStepCounter();
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 权限被拒绝: {status}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    PedometerStatus = "需要活动识别权限";
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 权限请求异常: {ex}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PedometerStatus = $"权限请求失败: {ex.Message}";
            });
        }
    }

    private void CreateStepCounter()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 调用工厂创建计步器");
            var context = Platform.CurrentActivity!;
            _stepCounter = StepCounterFactory.Create(context);

            if (_stepCounter == null)
            {
                PedometerStatus = "设备不支持计步，仅节拍器模式";
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 无计步传感器，仅节拍器模式");
                return;
            }

            // 订阅事件
            _stepCounter.OnStepCountChanged += OnStepCountChanged;
            _stepCounter.OnStepFrequencyChanged += OnStepFrequencyChanged;

            // 显示传感器类型
            string sensorType = _stepCounter is HardwareStepCounter ? "硬件传感器" : "加速度传感器";
            PedometerStatus = $"使用{sensorType}";

            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 计步器初始化成功 - {sensorType}");
        }
        catch (Exception ex)
        {
            PedometerStatus = $"初始化失败: {ex.Message}";
            Logger?.LogError(ex, "初始化计步器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 创建计步器失败: {ex}");
        }
    }

    private void OnStepCountChanged(int steps)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] 收到步数变化: {steps}");

        // 在主线程上更新UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StepCount = steps;
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] UI已更新步数: {steps}");
        });
    }

    private void OnStepFrequencyChanged(double spm)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] 收到步频变化: {spm:F1} SPM");

        // 在主线程上更新UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StepFrequency = spm;
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] UI已更新步频: {spm:F1}");
        });
    }

    [RelayCommand]
    private void NextTrack()
    {
        var context = Platform.CurrentActivity!;
        MediaController.NextTrack(context);
    }

    [RelayCommand]
    private void PreviousTrack()
    {
        var context = Platform.CurrentActivity!;
        MediaController.PreviousTrack(context);
    }

    [RelayCommand]
    private void PlayPause()
    {
        var context = Platform.CurrentActivity!;
        MediaController.PlayPause(context);
    }

    [RelayCommand]
    private void StartTick()
    {
        _cadencePlay.Start(DefaultTargetCadence);
    }

    [RelayCommand]
    private void StopTick()
    {
        _cadencePlay.Stop();
    }

    #region 运动会话

    public string SessionStatus
    {
        get => _sessionStatus;
        private set => SetProperty(ref _sessionStatus, value);
    }

    public string SessionMessage
    {
        get => _sessionMessage;
        private set => SetProperty(ref _sessionMessage, value);
    }

    public string SessionElapsed
    {
        get => _sessionElapsed;
        private set => SetProperty(ref _sessionElapsed, value);
    }

    public double SessionDistanceKm
    {
        get => _sessionDistanceKm;
        private set => SetProperty(ref _sessionDistanceKm, value);
    }

    public double SessionCadence
    {
        get => _sessionCadence;
        private set => SetProperty(ref _sessionCadence, value);
    }

    public int SessionTargetCadence
    {
        get => _sessionTargetCadence;
        private set => SetProperty(ref _sessionTargetCadence, value);
    }

    public string SessionSummary
    {
        get => _sessionSummary;
        private set => SetProperty(ref _sessionSummary, value);
    }

    /// <summary>false 表示无计步来源，界面隐藏实际步频。</summary>
    public bool CadenceAvailable
    {
        get => _cadenceAvailable;
        private set => SetProperty(ref _cadenceAvailable, value);
    }

    public bool CanStartSession => _sessionController != null && !_sessionController.IsActive;
    public bool CanPauseSession => _sessionController?.Session?.CanPause == true;
    public bool CanResumeSession => _sessionController?.Session?.CanResume == true;
    public bool CanStopSession => _sessionController?.Session?.CanStop == true;

    /// <summary>来源（GPS / 计步器）创建或切换后重建控制器；会话进行中不重建。</summary>
    private void RebuildSessionController()
    {
        if (_sessionController?.IsActive == true) return;

        if (_sessionController != null)
        {
            _sessionController.Updated -= OnSessionUpdated;
            _sessionController.Error -= OnSessionError;
            _sessionController.Dispose();
        }

        _sessionController = new RunSessionController(Platform.AppContext, _locationTracker, _stepCounter, _cadencePlay);
        _sessionController.Updated += OnSessionUpdated;
        _sessionController.Error += OnSessionError;
        CadenceAvailable = _sessionController.HasCadenceSource;
        RefreshSession();
    }

    private void OnSessionUpdated() => MainThread.BeginInvokeOnMainThread(RefreshSession);

    private void OnSessionError(string message)
    {
        Logger?.LogWarning("运动会话: {Message}", message);
        MainThread.BeginInvokeOnMainThread(() => SessionMessage = message);
    }

    private void RefreshSession()
    {
        var session = _sessionController?.Session;
        if (session == null)
        {
            SessionStatus = _sessionController == null ? "等待权限" : "未开始";
            SessionElapsed = "00:00:00";
            SessionDistanceKm = 0;
            SessionCadence = 0;
            SessionTargetCadence = DefaultTargetCadence;
            SessionSummary = "";
        }
        else
        {
            SessionStatus = session.State switch
            {
                RunSessionState.Running => "跑步中",
                RunSessionState.Paused => "已暂停",
                RunSessionState.Stopped => "已结束",
                _ => "未开始"
            };
            SessionElapsed = session.Elapsed.ToString(@"hh\:mm\:ss");
            SessionDistanceKm = session.DistanceKm;
            SessionCadence = session.CurrentCadence;
            SessionTargetCadence = session.TargetCadence;
            CadenceAvailable = session.CadenceAvailable;
            SessionSummary = session.CadenceAvailable
                ? $"平均步频 {session.AverageCadence:F0} · 步数 {session.StepCount} · 均速 {session.AverageSpeedKmh:F1} km/h"
                : $"均速 {session.AverageSpeedKmh:F1} km/h";
        }

        StartSessionCommand.NotifyCanExecuteChanged();
        PauseSessionCommand.NotifyCanExecuteChanged();
        ResumeSessionCommand.NotifyCanExecuteChanged();
        StopSessionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStartSession))]
    private async Task StartSessionAsync()
    {
        if (_sessionController == null) return;
        SessionMessage = "";
        try
        {
            await _sessionController.StartAsync(DefaultTargetCadence);
        }
        catch (Exception ex)
        {
            SessionMessage = $"启动失败: {ex.Message}";
            Logger?.LogError(ex, "启动运动会话失败");
        }
    }

    [RelayCommand(CanExecute = nameof(CanPauseSession))]
    private void PauseSession()
    {
        try
        {
            _sessionController?.Pause();
        }
        catch (Exception ex)
        {
            SessionMessage = $"暂停失败: {ex.Message}";
            Logger?.LogError(ex, "暂停运动会话失败");
        }
    }

    [RelayCommand(CanExecute = nameof(CanResumeSession))]
    private async Task ResumeSessionAsync()
    {
        if (_sessionController == null) return;
        try
        {
            await _sessionController.ResumeAsync();
        }
        catch (Exception ex)
        {
            SessionMessage = $"继续失败: {ex.Message}";
            Logger?.LogError(ex, "继续运动会话失败");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopSession))]
    private void StopSession()
    {
        try
        {
            _sessionController?.Stop();
        }
        catch (Exception ex)
        {
            SessionMessage = $"结束失败: {ex.Message}";
            Logger?.LogError(ex, "结束运动会话失败");
        }
    }

    #endregion

    #region 计步器相关属性

    public int StepCount
    {
        get => _stepCount;
        private set => SetProperty(ref _stepCount, value);
    }

    public double StepFrequency
    {
        get => _stepFrequency;
        private set => SetProperty(ref _stepFrequency, value);
    }

    public string PedometerStatus
    {
        get => _pedometerStatus;
        private set => SetProperty(ref _pedometerStatus, value);
    }

    #endregion

    #region 计步器相关命令

    [RelayCommand]
    private void StartPedometer()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 开始计步命令");

        if (_stepCounter == null || _isPedometerActive)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 无法启动 - 计步器为空: {_stepCounter == null}, 已激活: {_isPedometerActive}");
            return;
        }

        try
        {
            _stepCounter.Start();
            _isPedometerActive = true;
            PedometerStatus = _stepCounter is HardwareStepCounter
                ? "运行中（硬件可能先确认多步再上报）"
                : "运行中（加速度兜底，精度需实测）";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 计步器已启动");
        }
        catch (Exception ex)
        {
            PedometerStatus = $"启动失败: {ex.Message}";
            Logger?.LogError(ex, "启动计步器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 启动失败: {ex}");
        }
    }

    [RelayCommand]
    private void StopPedometer()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 停止计步命令");

        if (_stepCounter == null || !_isPedometerActive)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 无法停止 - 计步器为空: {_stepCounter == null}, 未激活: {!_isPedometerActive}");
            return;
        }

        try
        {
            _stepCounter.Stop();
            _isPedometerActive = false;

            string sensorType = _stepCounter is HardwareStepCounter ? "硬件传感器" : "加速度传感器";
            PedometerStatus = $"使用{sensorType}";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 计步器已停止");
        }
        catch (Exception ex)
        {
            PedometerStatus = $"停止失败: {ex.Message}";
            Logger?.LogError(ex, "停止计步器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 停止失败: {ex}");
        }
    }

    [RelayCommand]
    private void ResetStepCount()
    {
        _stepCounter?.Reset();
        StepCount = 0;
        StepFrequency = 0.0;
    }

    [RelayCommand]
    private void SwitchToAccelerometer() => SwitchStepCounter(forceAccelerometer: true);

    [RelayCommand]
    private void SwitchToHardware() => SwitchStepCounter(forceAccelerometer: false);

    private void SwitchStepCounter(bool forceAccelerometer)
    {
        string target = forceAccelerometer ? "加速度传感器" : "硬件传感器";
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] 切换到{target}");

        if (_sessionController?.IsActive == true)
        {
            PedometerStatus = "会话进行中，不能切换传感器";
            return;
        }

        try
        {
            // 停止并释放当前计步器
            if (_stepCounter != null)
            {
                if (_isPedometerActive)
                {
                    _stepCounter.Stop();
                    _isPedometerActive = false;
                }

                _stepCounter.OnStepCountChanged -= OnStepCountChanged;
                _stepCounter.OnStepFrequencyChanged -= OnStepFrequencyChanged;
                _stepCounter.Dispose();
                _stepCounter = null;
            }

            StepCounterFactory.ForceAccelerometer = forceAccelerometer;
            CreateStepCounter();
            RebuildSessionController();

            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 已切换到{target}");
        }
        catch (Exception ex)
        {
            PedometerStatus = $"切换失败: {ex.Message}";
            Logger?.LogError(ex, "切换到{Target}失败", target);
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 切换失败: {ex}");
        }
    }

    #endregion

    #region GPS 距离追踪

    private async Task RequestLocationPermission()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 检查位置权限");

            var status = await Permissions.CheckStatusAsync<LocationPermission>();
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 当前位置权限状态: {status}");

            if (status != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 请求位置权限");
                status = await Permissions.RequestAsync<LocationPermission>();
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 位置权限请求结果: {status}");
            }

            if (status == PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 位置权限已授予，创建GPS追踪器");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CreateLocationTracker();
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 位置权限被拒绝: {status}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    GpsStatus = "需要位置权限";
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 位置权限请求异常: {ex}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                GpsStatus = $"权限请求失败: {ex.Message}";
            });
        }
    }

    private void CreateLocationTracker()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] 创建GPS追踪器");
            var context = Platform.CurrentActivity!;

            _locationTracker = new AndroidLocationTracker(context);

            // 订阅事件
            _locationTracker.OnDistanceUpdated += OnGpsDistanceUpdated;
            _locationTracker.OnSpeedUpdated += OnGpsSpeedUpdated;
            _locationTracker.OnError += OnGpsError;
            _locationTracker.OnStatusUpdated += OnGpsStatusUpdated;

            GpsStatus = "已就绪";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪器创建成功");
        }
        catch (Exception ex)
        {
            GpsStatus = $"创建失败: {ex.Message}";
            Logger?.LogError(ex, "创建GPS追踪器失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 创建GPS追踪器失败: {ex}");
        }
    }

    private void OnGpsDistanceUpdated(double distanceMeters)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS距离更新: {distanceMeters:F1}米");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            GpsDistance = distanceMeters / 1000.0; // 转换为公里
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] UI已更新GPS距离: {GpsDistance:F2}公里");
        });
    }

    private void OnGpsSpeedUpdated(double speedKmh)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS速度更新: {speedKmh:F1}公里/小时");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            GpsSpeed = speedKmh;
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] UI已更新GPS速度: {GpsSpeed:F1}公里/小时");
        });
    }

    private void OnGpsError(string error)
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS错误: {error}");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            GpsStatus = $"错误: {error}";
        });
    }

    private void OnGpsStatusUpdated(string status)
    {
        MainThread.BeginInvokeOnMainThread(() => GpsStatus = status);
    }

    public double GpsDistance
    {
        get => _gpsDistance;
        private set => SetProperty(ref _gpsDistance, value);
    }

    public double GpsSpeed
    {
        get => _gpsSpeed;
        private set => SetProperty(ref _gpsSpeed, value);
    }

    public string GpsStatus
    {
        get => _gpsStatus;
        private set => SetProperty(ref _gpsStatus, value);
    }

    [RelayCommand]
    private async Task StartGpsTracking()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 开始GPS追踪命令");

        if (_locationTracker == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪器未初始化");
            GpsStatus = "追踪器未初始化";
            return;
        }

        if (_locationTracker.IsTracking)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS已在追踪中");
            return;
        }

        try
        {
            await _locationTracker.StartAsync();
            GpsStatus = "正在定位，请到开阔处";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪已启动");
        }
        catch (Exception ex)
        {
            GpsStatus = $"启动失败: {ex.Message}";
            Logger?.LogError(ex, "启动GPS追踪失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS启动失败: {ex}");
        }
    }

    [RelayCommand]
    private void StopGpsTracking()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 停止GPS追踪命令");

        if (_locationTracker == null || !_locationTracker.IsTracking)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS未在追踪中");
            return;
        }

        try
        {
            _locationTracker.Stop();
            GpsStatus = "已停止";
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪已停止");
        }
        catch (Exception ex)
        {
            GpsStatus = $"停止失败: {ex.Message}";
            Logger?.LogError(ex, "停止GPS追踪失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS停止失败: {ex}");
        }
    }

    [RelayCommand]
    private void ResetGpsDistance()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] 重置GPS距离命令");

        if (_locationTracker == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS追踪器未初始化");
            return;
        }

        try
        {
            _locationTracker.Reset();
            GpsDistance = 0;
            GpsSpeed = 0;
            System.Diagnostics.Debug.WriteLine("[MainViewModel] GPS距离已重置");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "重置GPS距离失败");
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] GPS重置失败: {ex}");
        }
    }

    #endregion

    [RelayCommand]
    private async Task GoToWatchWorkout()
    {
        await Navigation.NavigateAsync<WatchWorkoutViewModel>();
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        // 先结束会话：控制器会停掉来源和前台服务。
        if (_sessionController != null)
        {
            _sessionController.Updated -= OnSessionUpdated;
            _sessionController.Error -= OnSessionError;
            _sessionController.Dispose();
            _sessionController = null;
        }

        _cadencePlay?.Dispose();

        if (_stepCounter != null)
        {
            _stepCounter.OnStepCountChanged -= OnStepCountChanged;
            _stepCounter.OnStepFrequencyChanged -= OnStepFrequencyChanged;
            _stepCounter.Dispose();
            _stepCounter = null;
        }

        if (_locationTracker != null)
        {
            _locationTracker.OnDistanceUpdated -= OnGpsDistanceUpdated;
            _locationTracker.OnSpeedUpdated -= OnGpsSpeedUpdated;
            _locationTracker.OnError -= OnGpsError;
            _locationTracker.OnStatusUpdated -= OnGpsStatusUpdated;
            _locationTracker.Dispose();
            _locationTracker = null;
        }

        base.Dispose(disposing);

    }

}

using Microsoft.Extensions.Logging;
using Runner.Business.Location;
using Runner.Business.Platforms.Android.Location;
using Runner.Business.Platforms.Android.Media;
using Runner.Business.Platforms.Android.Permissions;
using Runner.Business.Platforms.Android.StepCounter;
using Runner.Business.Services.Workouts;
using Runner.Core.Workouts;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;

namespace Runner.Business.Platforms.Android.Session;

/// <summary>
/// 应用级单例：申请权限、创建并持有 GPS / 计步 / 节拍器来源和会话控制器，记住当前选中的课程。
/// 页面只消费它，不各自创建监听器。
/// </summary>
public sealed class RunSessionHost : IDisposable
{
    private readonly int _tickSoundResourceId;
    private readonly ILogger<RunSessionHost> _logger;
    private readonly CadencePlay _metronome = new();
    private Task? _initialization;
    private IStepCounter? _stepCounter;
    private ILocationTracker? _locationTracker;
    private RunSessionController? _controller;

    /// <summary>会话指标变化、来源重建或状态文本变化时触发，调用线程不定。</summary>
    public event Action? Updated;
    public event Action<string>? Error;

    public RunSessionHost(int tickSoundResourceId, WorkoutStore workouts, ILogger<RunSessionHost> logger)
    {
        _tickSoundResourceId = tickSoundResourceId;
        Workouts = workouts;
        _logger = logger;
    }

    public WorkoutStore Workouts { get; }
    /// <summary>首页选中的课程；null 为自由跑。</summary>
    public WorkoutPlan? SelectedWorkout { get; set; }
    public bool IsInitialized => _controller != null;
    public RunSessionController? Controller => _controller;
    public IStepCounter? StepCounter => _stepCounter;
    public ILocationTracker? LocationTracker => _locationTracker;
    public CadencePlay Metronome => _metronome;
    public string StepCounterStatus { get; private set; } = "未初始化";
    public string LocationStatus { get; private set; } = "未初始化";
    public bool IsSessionActive => _controller?.IsActive == true;

    /// <summary>串行申请权限并创建来源。必须在主线程调用；重复调用只执行一次。</summary>
    public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        // 音频资源加载不阻塞 UI 线程
        _ = Task.Run(() => _metronome.Init(_tickSoundResourceId));

        await CreateStepCounterAsync(forceAccelerometer: false);
        await CreateLocationTrackerAsync();

        // 前台服务的通知需要此权限；服务本身随会话起停。
        try
        {
            await MauiPermissions.RequestAsync<MauiPermissions.PostNotifications>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "请求通知权限失败");
        }

        RebuildController();
    }

    /// <summary>诊断用：强制切换计步来源。会话进行中返回 false。</summary>
    public async Task<bool> SwitchStepCounterAsync(bool forceAccelerometer)
    {
        if (IsSessionActive) return false;
        DisposeStepCounter();
        await CreateStepCounterAsync(forceAccelerometer);
        RebuildController();
        return true;
    }

    private async Task CreateStepCounterAsync(bool forceAccelerometer)
    {
        try
        {
            var status = await MauiPermissions.CheckStatusAsync<ActivityRecognitionPermission>();
            if (status != PermissionStatus.Granted)
                status = await MauiPermissions.RequestAsync<ActivityRecognitionPermission>();
            if (status != PermissionStatus.Granted)
            {
                StepCounterStatus = "需要活动识别权限";
                return;
            }

            StepCounterFactory.ForceAccelerometer = forceAccelerometer;
            _stepCounter = StepCounterFactory.Create(Platform.AppContext);
            StepCounterStatus = _stepCounter switch
            {
                null => "设备不支持计步，仅节拍器模式",
                HardwareStepCounter => "硬件传感器",
                _ => "加速度传感器"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化计步器失败");
            StepCounterStatus = $"初始化失败: {ex.Message}";
        }
    }

    private async Task CreateLocationTrackerAsync()
    {
        try
        {
            var status = await MauiPermissions.CheckStatusAsync<LocationPermission>();
            if (status != PermissionStatus.Granted)
                status = await MauiPermissions.RequestAsync<LocationPermission>();
            if (status != PermissionStatus.Granted)
            {
                LocationStatus = "需要位置权限";
                return;
            }

            _locationTracker = new AndroidLocationTracker(Platform.AppContext);
            LocationStatus = "已就绪";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建GPS追踪器失败");
            LocationStatus = $"创建失败: {ex.Message}";
        }
    }

    private void RebuildController()
    {
        if (_controller != null)
        {
            _controller.Updated -= OnControllerUpdated;
            _controller.Error -= OnControllerError;
            _controller.Dispose();
        }

        _controller = new RunSessionController(Platform.AppContext, _locationTracker, _stepCounter, _metronome);
        _controller.Updated += OnControllerUpdated;
        _controller.Error += OnControllerError;
        Updated?.Invoke();
    }

    private void OnControllerUpdated() => Updated?.Invoke();

    private void OnControllerError(string message)
    {
        _logger.LogWarning("运动会话: {Message}", message);
        Error?.Invoke(message);
    }

    private void DisposeStepCounter()
    {
        _stepCounter?.Dispose();
        _stepCounter = null;
    }

    public void Dispose()
    {
        if (_controller != null)
        {
            _controller.Updated -= OnControllerUpdated;
            _controller.Error -= OnControllerError;
            _controller.Dispose();
            _controller = null;
        }
        DisposeStepCounter();
        _locationTracker?.Dispose();
        _locationTracker = null;
        _metronome.Dispose();
        Updated = null;
        Error = null;
    }
}

using Microsoft.Extensions.Logging;
using Runner.Business.Location;
using Runner.Business.Media;
using Runner.Business.Services.Activities;
using Runner.Business.Services.Workouts;
using Runner.Business.StepCounter;
using Runner.Core.Activities;
using Runner.Core.Session;
using Runner.Core.Workouts;

namespace Runner.Business.Session;

/// <summary>应用级运动宿主：共用课程、会话、保存流程；原生来源和权限由平台实现提供。</summary>
public sealed class RunSessionHost : IDisposable
{
    private const string PreferredWorkoutKey = "workout.selected";
    private readonly IRunPlatform _platform;
    private readonly ILogger<RunSessionHost> _logger;
    private readonly ICadencePlayer _metronome;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _audioInitialized;
    private bool _disposed;
    private IStepCounter? _stepCounter;
    private ILocationTracker? _locationTracker;
    private RunSessionController? _controller;

    public event Action? Updated;
    public event Action<string>? Error;
    public event Action<string>? Notice;

    public RunSessionHost(IRunPlatform platform, WorkoutStore workouts, ActivityStore activities, ILogger<RunSessionHost> logger)
    {
        _platform = platform;
        _metronome = platform.CreateCadencePlayer();
        Workouts = workouts;
        Activities = activities;
        _logger = logger;
    }

    public WorkoutStore Workouts { get; }
    public ActivityStore Activities { get; }
    public WorkoutPlan? SelectedWorkout { get; set; }
    public int PreferredWorkoutId
    {
        get => Preferences.Default.Get(PreferredWorkoutKey, 0);
        set => Preferences.Default.Set(PreferredWorkoutKey, value);
    }

    public bool IsInitialized => _controller != null;
    public RunSessionController? Controller => _controller;
    public IStepCounter? StepCounter => _stepCounter;
    public ILocationTracker? LocationTracker => _locationTracker;
    public ICadencePlayer Metronome => _metronome;
    public string StepCounterStatus { get; private set; } = "未初始化";
    public string LocationStatus { get; private set; } = "未初始化";
    public bool IsSessionActive => _controller?.IsActive == true;
    public bool CanSwitchStepCounter => _platform.CanSwitchStepCounter;
    public bool CanControlMusic => _platform.CanControlMusic;
    public string MusicControlStatus => _platform.MusicControlStatus;
    /// <summary>最近一次 Error 的完整异常，供界面按需复制；不写入运动记录。</summary>
    public string LastErrorDetails { get; private set; } = "";

    public void NextTrack() => _platform.NextTrack();
    public void PreviousTrack() => _platform.PreviousTrack();
    public void PlayPause() => _platform.PlayPause();

    /// <summary>从主线程调用。只串行化初始化，不把一次权限拒绝缓存成终身缺失的来源。</summary>
    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _initializationGate.WaitAsync();
        try
        {
            if (IsSessionActive) return;
            await InitializeAudioAsync();
            var previousSteps = _stepCounter;
            var previousLocation = _locationTracker;
            if (_stepCounter == null) await CreateStepCounterAsync(false);
            if (_locationTracker == null) await CreateLocationTrackerAsync();
            try { await _platform.PrepareAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "准备平台运动服务失败"); }

            if (_controller == null || previousSteps != _stepCounter || previousLocation != _locationTracker)
                RebuildController();
            else
                Updated?.Invoke();
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private async Task InitializeAudioAsync()
    {
        if (_audioInitialized) return;
        try
        {
            await _metronome.InitializeAsync();
            _audioInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载节拍音失败");
            ReportError($"加载节拍音失败: {ex.Message}", ex);
        }
    }

    public async Task<bool> SwitchStepCounterAsync(bool forceAccelerometer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _initializationGate.WaitAsync();
        try
        {
            if (IsSessionActive || !CanSwitchStepCounter) return false;
            DisposeStepCounter();
            await CreateStepCounterAsync(forceAccelerometer);
            RebuildController();
            return true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private async Task CreateStepCounterAsync(bool forceAccelerometer)
    {
        try
        {
            (_stepCounter, StepCounterStatus) = await _platform.CreateStepCounterAsync(forceAccelerometer);
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
            (_locationTracker, LocationStatus) = await _platform.CreateLocationTrackerAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建 GPS 追踪器失败");
            LocationStatus = $"创建失败: {ex.Message}";
        }
    }

    private void RebuildController()
    {
        DetachController();
        _controller = new RunSessionController(_platform, _locationTracker, _stepCounter, _metronome);
        _controller.Updated += OnControllerUpdated;
        _controller.Error += OnControllerError;
        _controller.Stopped += OnSessionStopped;
        Updated?.Invoke();
    }

    private void OnControllerUpdated() => Updated?.Invoke();

    private void OnControllerError(string message)
    {
        _logger.LogWarning("运动会话: {Message}", message);
        ReportError(message);
    }

    private void ReportError(string message, Exception? exception = null)
    {
        LastErrorDetails = exception?.ToString() ?? message;
        Error?.Invoke(message);
    }

    private void OnSessionStopped(RunSession session, WorkoutRunner? workout)
    {
        if (!ActivityRecord.ShouldRecord(session)) return;
        _ = SaveActivityAsync(ActivityRecord.FromSession(session, workout?.Name));
    }

    private async Task SaveActivityAsync(ActivityRecord record)
    {
        try
        {
            await Activities.SaveAsync(record);
            LastErrorDetails = "";
            Notice?.Invoke("记录已保存");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存记录失败");
            ReportError($"保存记录失败: {ex.Message}", ex);
        }
    }

    private void DisposeStepCounter()
    {
        _stepCounter?.Dispose();
        _stepCounter = null;
    }

    private void DetachController()
    {
        if (_controller == null) return;
        // Dispose 会结束活动会话；先保留 Stopped 订阅，避免释放宿主时漏掉保存。
        _controller.Dispose();
        _controller.Updated -= OnControllerUpdated;
        _controller.Error -= OnControllerError;
        _controller.Stopped -= OnSessionStopped;
        _controller = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetachController();
        DisposeStepCounter();
        _locationTracker?.Dispose();
        _locationTracker = null;
        _metronome.Dispose();
        Updated = null;
        Error = null;
        Notice = null;
    }
}

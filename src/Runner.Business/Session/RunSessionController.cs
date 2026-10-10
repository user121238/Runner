using Runner.Business.Diagnostics;
using Runner.Business.Location;
using Runner.Business.Media;
using Runner.Business.StepCounter;
using Runner.Core.Geolocation;
using Runner.Core.Session;
using Runner.Core.Workouts;

namespace Runner.Business.Session;

/// <summary>
/// 一个 Start / Pause / Stop 驱动 GPS、计步、节拍器与前台服务；带课程时按阶段切换节拍、按需切歌。
/// 来源对象由调用方在权限授予后创建并持有，控制器只在会话期间启停它们；
/// 单个来源启动失败不终止会话，通过 <see cref="Error"/> 上报。
/// </summary>
public sealed class RunSessionController : IDisposable
{
    private readonly IRunPlatform _platform;
    private readonly ILocationTracker? _gps;
    private readonly IStepCounter? _steps;
    private readonly ICadencePlayer _metronome;
    private readonly object _sync = new();
    private Timer? _ticker;
    private RunSession? _session;
    private WorkoutRunner? _workout;

    /// <summary>指标变化、阶段切换或每秒计时时触发，调用线程不定，订阅方自行切主线程。</summary>
    public event Action? Updated;
    /// <summary>某个来源或前台服务操作失败，会话继续。</summary>
    public event Action<string>? Error;
    /// <summary>会话结束、来源已停后触发，供落盘记录。</summary>
    public event Action<RunSession, WorkoutRunner?>? Stopped;

    public RunSessionController(IRunPlatform platform, ILocationTracker? gps, IStepCounter? steps, ICadencePlayer metronome)
    {
        _platform = platform;
        _gps = gps;
        _steps = steps;
        _metronome = metronome;
        _metronome.OnError += OnSourceError;
        if (_gps != null)
        {
            _gps.OnDistanceUpdated += OnDistance;
            _gps.OnLocationUpdated += OnLocation;
            _gps.OnError += OnSourceError;
        }
        if (_steps != null)
        {
            _steps.OnStepCountChanged += OnSteps;
            _steps.OnStepFrequencyChanged += OnCadence;
            _steps.OnError += OnSourceError;
        }
    }

    public RunSession? Session => _session;
    /// <summary>本次会话的课程推进器；自由跑为 null。</summary>
    public WorkoutRunner? Workout => _workout;
    public bool HasGps => _gps != null;
    public bool HasCadenceSource => _steps != null;
    public bool IsActive => _session?.State is RunSessionState.Running or RunSessionState.Paused;

    /// <summary>开始一次会话；plan 为 null 时自由跑，不播节拍。</summary>
    public async Task StartAsync(WorkoutPlan? plan)
    {
        int targetCadence = 0;
        bool switchTrack = false;
        lock (_sync)
        {
            if (IsActive) throw new InvalidOperationException("会话已在进行中");

            _workout = plan == null ? null : new WorkoutRunner(plan);
            if (_workout != null)
            {
                _workout.Start(TimeSpan.Zero, 0);
                var stage = _workout.Current!;
                targetCadence = stage.TargetCadence;
                switchTrack = stage.SwitchTrackOnEnter;
            }

            var session = new RunSession(_platform.Clock);
            session.Start(targetCadence, cadenceAvailable: _steps != null);
            _session = session;
        }
        DiagnosticLog.Write("会话", $"开始 课程={plan?.Name ?? "自由跑"} 目标步频={targetCadence} GPS={HasGps} 计步={HasCadenceSource}");
        _gps?.Reset();
        _steps?.Reset();
        if (!_platform.CanControlMusic && plan?.Stages.Any(stage => stage.SwitchTrackOnEnter) == true)
            Error?.Invoke(_platform.MusicControlStatus);
        Try(() => _platform.StartBackgroundSession(HasGps), "后台运动服务启动");
        await StartSourcesAsync(targetCadence);
        if (switchTrack && _platform.CanControlMusic) Try(NextTrack, "切歌");
        StartTicker();
        Updated?.Invoke();
    }

    public void Pause()
    {
        // 先切状态再停来源，停止过程中的尾部回调会被会话忽略。
        lock (_sync)
        {
            RequireSession().Pause();
        }
        DiagnosticLog.Write("会话", "暂停");
        StopSources();
        StopTicker();
        Updated?.Invoke();
    }

    public async Task ResumeAsync()
    {
        int targetCadence;
        lock (_sync)
        {
            var session = RequireSession();
            session.Resume();
            targetCadence = session.TargetCadence;
        }
        DiagnosticLog.Write("会话", "继续");
        // 暂停期间可能重新开启了定位；Android 需同步升级前台服务的实际类型。
        Try(() => _platform.StartBackgroundSession(HasGps), "后台运动服务恢复");
        await StartSourcesAsync(targetCadence);
        StartTicker();
        Updated?.Invoke();
    }

    public void Stop()
    {
        RunSession session;
        lock (_sync)
        {
            session = RequireSession();
            session.Stop();
        }
        DiagnosticLog.Write("会话", $"结束 时长={session.Elapsed.TotalSeconds:F0}s 距离={session.DistanceMeters:F0}m 步数={session.StepCount}");
        StopSources();
        StopTicker();
        Try(_platform.StopBackgroundSession, "后台运动服务停止");
        Updated?.Invoke();
        Stopped?.Invoke(session, _workout);
    }

    private RunSession RequireSession() => _session ?? throw new InvalidOperationException("没有进行中的会话");

    private async Task StartSourcesAsync(int targetCadence)
    {
        if (_gps != null)
        {
            try { await _gps.StartAsync(); }
            catch (Exception ex)
            {
                DiagnosticLog.Write("会话", $"GPS 启动失败: {ex.Message}");
                Error?.Invoke($"GPS 启动失败: {ex.Message}");
            }
        }
        if (_steps != null)
            Try(_steps.Start, "计步启动");
        ApplyCadence(targetCadence);
    }

    private void StopSources()
    {
        Try(_metronome.Stop, "节拍停止");
        if (_steps != null) Try(_steps.Stop, "计步停止");
        if (_gps != null) Try(_gps.Stop, "GPS 停止");
    }

    private void ApplyCadence(int targetCadence)
    {
        if (targetCadence > 0) Try(() => _metronome.Start(targetCadence), "节拍启动");
        else Try(_metronome.Stop, "节拍停止");
    }

    /// <summary>用会话的跑动时长与距离推进课程：进入新阶段换节拍、按需切歌；课程完成停节拍。</summary>
    private void AdvanceWorkout()
    {
        WorkoutStage? entered = null;
        bool completed = false;
        lock (_sync)
        {
            if (_workout == null || _session?.State != RunSessionState.Running) return;
            switch (_workout.Update(_session.Elapsed, _session.DistanceMeters))
            {
                case WorkoutProgressEvent.StageStarted:
                    entered = _workout.Current;
                    _session.SetTargetCadence(entered!.TargetCadence);
                    break;
                case WorkoutProgressEvent.Completed:
                    completed = true;
                    _session.SetTargetCadence(0);
                    break;
                default:
                    return;
            }
        }

        if (entered != null)
        {
            DiagnosticLog.Write("会话", $"进入阶段 步频={entered.TargetCadence} 切歌={entered.SwitchTrackOnEnter}");
            ApplyCadence(entered.TargetCadence);
            if (entered.SwitchTrackOnEnter && _platform.CanControlMusic) Try(NextTrack, "切歌");
        }
        else if (completed)
        {
            DiagnosticLog.Write("会话", "课程完成");
            _metronome.Stop();
        }
        Updated?.Invoke();
    }

    private void NextTrack() => _platform.NextTrack();

    private void OnSourceError(string message)
    {
        DiagnosticLog.Write("来源", message);
        Error?.Invoke(message);
    }

    private void StartTicker()
    {
        lock (_sync)
        {
            _ticker ??= new Timer(_ =>
            {
                // 与页面的暂停/停止串行，原生音频控制也在主线程上执行。
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AdvanceWorkout();
                    Updated?.Invoke();
                });
            }, null, 1000, 1000);
        }
    }

    private void StopTicker()
    {
        lock (_sync)
        {
            _ticker?.Dispose();
            _ticker = null;
        }
    }

    private void Try(Action action, string description)
    {
        try { action(); }
        catch (Exception ex)
        {
            DiagnosticLog.Write("会话", $"{description}失败: {ex.Message}");
            Error?.Invoke($"{description}失败: {ex.Message}");
        }
    }

    private void OnDistance(double totalMeters)
    {
        _session?.UpdateDistance(totalMeters);
        MainThread.BeginInvokeOnMainThread(AdvanceWorkout);
        Updated?.Invoke();
    }

    private void OnLocation(GeoCoordinate point) => _session?.AddTrackPoint(point);

    private void OnSteps(int totalSteps)
    {
        _session?.UpdateStepCount(totalSteps);
        Updated?.Invoke();
    }

    private void OnCadence(double stepsPerMinute)
    {
        _session?.UpdateCadence(stepsPerMinute);
        Updated?.Invoke();
    }

    public void Dispose()
    {
        if (IsActive) Try(Stop, "结束会话");
        StopTicker();
        if (_gps != null)
        {
            _gps.OnDistanceUpdated -= OnDistance;
            _gps.OnLocationUpdated -= OnLocation;
            _gps.OnError -= OnSourceError;
        }
        if (_steps != null)
        {
            _steps.OnStepCountChanged -= OnSteps;
            _steps.OnStepFrequencyChanged -= OnCadence;
            _steps.OnError -= OnSourceError;
        }
        _metronome.OnError -= OnSourceError;
        Updated = null;
        Error = null;
        Stopped = null;
    }
}

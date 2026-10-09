using Android.Content;
using Android.OS;
using Runner.Business.Location;
using Runner.Business.Models;
using Runner.Business.Platforms.Android.Alerts;
using Runner.Business.Platforms.Android.Media;
using Runner.Business.Platforms.Android.Services;
using Runner.Business.Platforms.Android.StepCounter;
using Runner.Core.Alerts;
using Runner.Core.Geolocation;
using Runner.Core.Session;
using Runner.Core.SignalProcessing;

namespace Runner.Business.Platforms.Android.Session;

/// <summary>
/// 一个 Start / Pause / Stop 驱动 GPS、计步、节拍器与前台服务，跑动中判定步频偏离并触发提醒。
/// 来源对象由调用方在权限授予后创建并持有，控制器只在会话期间启停它们；
/// 单个来源启动失败不终止会话，通过 <see cref="Error"/> 上报。
/// </summary>
public sealed class RunSessionController : IDisposable
{
    private readonly Context _context;
    private readonly ILocationTracker? _gps;
    private readonly IStepCounter? _steps;
    private readonly CadencePlay _metronome;
    private readonly CadenceAlertNotifier _alerts;
    private readonly SensorClock _clock = new(SystemClock.ElapsedRealtimeNanos);
    private readonly object _sync = new();
    private Timer? _ticker;
    private RunSession? _session;
    private CadenceAlertMonitor? _monitor;

    /// <summary>指标变化或每秒计时时触发，调用线程不定，订阅方自行切主线程。</summary>
    public event Action? Updated;
    /// <summary>某个来源或前台服务操作失败，会话继续。</summary>
    public event Action<string>? Error;

    public RunSessionController(Context context, ILocationTracker? gps, IStepCounter? steps,
        CadencePlay metronome, CadenceAlertNotifier alerts)
    {
        _context = context.ApplicationContext ?? context;
        _gps = gps;
        _steps = steps;
        _metronome = metronome;
        _alerts = alerts;
        if (_gps != null)
        {
            _gps.OnDistanceUpdated += OnDistance;
            _gps.OnLocationUpdated += OnLocation;
        }
        if (_steps != null)
        {
            _steps.OnStepCountChanged += OnSteps;
            _steps.OnStepFrequencyChanged += OnCadence;
        }
    }

    public RunSession? Session => _session;
    /// <summary>当前步频偏离状态，供界面着色；暂停、结束时为 None。</summary>
    public CadenceAlertKind AlertState { get; private set; }
    public bool HasGps => _gps != null;
    public bool HasCadenceSource => _steps != null;
    public bool IsActive => _session?.State is RunSessionState.Running or RunSessionState.Paused;

    public async Task StartAsync(RunSettings settings)
    {
        RunSession session;
        lock (_sync)
        {
            if (IsActive) throw new InvalidOperationException("会话已在进行中");
            session = new RunSession(_clock);
            session.Start(settings.TargetCadence, cadenceAvailable: _steps != null);
            _session = session;
            _monitor = new CadenceAlertMonitor(new CadenceAlertConfig
            {
                TargetCadence = settings.TargetCadence,
                ToleranceSpm = settings.ToleranceSpm
            }, _clock);
            _alerts.Apply(settings);
        }
        SetAlertState(CadenceAlertKind.None);
        _gps?.Reset();
        _steps?.Reset();
        Try(StartForegroundService, "前台服务启动");
        await StartSourcesAsync(settings.TargetCadence);
        StartTicker();
        Updated?.Invoke();
    }

    public void Pause()
    {
        // 先切状态再停来源，停止过程中的尾部回调会被会话忽略。
        lock (_sync)
        {
            RequireSession().Pause();
            _monitor?.Reset();
        }
        SetAlertState(CadenceAlertKind.None);
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
            _monitor?.Reset();
            targetCadence = session.TargetCadence;
        }
        await StartSourcesAsync(targetCadence);
        StartTicker();
        Updated?.Invoke();
    }

    public void Stop()
    {
        lock (_sync)
        {
            RequireSession().Stop();
            _monitor?.Reset();
        }
        SetAlertState(CadenceAlertKind.None);
        StopSources();
        StopTicker();
        Try(StopForegroundService, "前台服务停止");
        Updated?.Invoke();
    }

    private RunSession RequireSession() => _session ?? throw new InvalidOperationException("没有进行中的会话");

    private async Task StartSourcesAsync(int targetCadence)
    {
        if (_gps != null)
        {
            try { await _gps.StartAsync(); }
            catch (Exception ex) { Error?.Invoke($"GPS 启动失败: {ex.Message}"); }
        }
        if (_steps != null)
            Try(_steps.Start, "计步启动");
        _metronome.Start(targetCadence);
    }

    private void StopSources()
    {
        _metronome.Stop();
        if (_steps != null) Try(_steps.Stop, "计步停止");
        if (_gps != null) Try(_gps.Stop, "GPS 停止");
    }

    private void StartForegroundService() =>
        _context.StartForegroundService(new Intent(_context, typeof(RunnerService)));

    private void StopForegroundService() =>
        _context.StopService(new Intent(_context, typeof(RunnerService)));

    private void StartTicker()
    {
        lock (_sync)
        {
            _ticker ??= new Timer(_ => Updated?.Invoke(), null, 1000, 1000);
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

    private void SetAlertState(CadenceAlertKind state)
    {
        if (AlertState == state) return;
        AlertState = state;
        _alerts.SetState(state);
    }

    private void Try(Action action, string description)
    {
        try { action(); }
        catch (Exception ex) { Error?.Invoke($"{description}失败: {ex.Message}"); }
    }

    private void OnDistance(double totalMeters)
    {
        _session?.UpdateDistance(totalMeters);
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
        var alert = CadenceAlertKind.None;
        var state = CadenceAlertKind.None;
        lock (_sync)
        {
            _session?.UpdateCadence(stepsPerMinute);
            if (_session?.State == RunSessionState.Running && _monitor != null)
            {
                alert = _monitor.Evaluate(stepsPerMinute);
                state = _monitor.State;
            }
        }
        SetAlertState(state);
        if (alert != CadenceAlertKind.None) _alerts.Notify(alert);
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
        }
        if (_steps != null)
        {
            _steps.OnStepCountChanged -= OnSteps;
            _steps.OnStepFrequencyChanged -= OnCadence;
        }
        Updated = null;
        Error = null;
    }
}

using Runner.Core.Geolocation;

namespace Runner.Core.Session;

/// <summary>
/// 一次跑步的会话：时长只累计 Running 区间；距离、步数、步频和轨迹只在 Running 时接收输入。
/// 距离与步数是外部追踪器的累计值，会话只做镜像与时间记账，不重复过滤。
/// 会话一次性使用，Stop 后为终态。
/// </summary>
public sealed class RunSession
{
    private readonly TimeProvider _clock;
    private readonly object _lock = new();
    private readonly List<GeoCoordinate> _trackPoints = new();
    private readonly List<CadenceSample> _cadenceSamples = new();
    private TimeSpan _accumulated;
    private DateTimeOffset _segmentStart;

    public RunSession(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public RunSessionState State { get; private set; } = RunSessionState.Idle;
    public DateTimeOffset? StartTime { get; private set; }
    public DateTimeOffset? EndTime { get; private set; }
    public int TargetCadence { get; private set; }
    /// <summary>false 表示设备没有计步来源，只播节拍，界面不显示实际步频。</summary>
    public bool CadenceAvailable { get; private set; } = true;
    public double DistanceMeters { get; private set; }
    public double DistanceKm => DistanceMeters / 1000;
    public int StepCount { get; private set; }
    public double CurrentCadence { get; private set; }

    public bool CanStart => State == RunSessionState.Idle;
    public bool CanPause => State == RunSessionState.Running;
    public bool CanResume => State == RunSessionState.Paused;
    public bool CanStop => State is RunSessionState.Running or RunSessionState.Paused;

    /// <summary>跑动时长，不含暂停。</summary>
    public TimeSpan Elapsed
    {
        get
        {
            lock (_lock)
            {
                return State == RunSessionState.Running
                    ? _accumulated + (_clock.GetUtcNow() - _segmentStart)
                    : _accumulated;
            }
        }
    }

    /// <summary>平均步频 = 步数 / 跑动分钟数。</summary>
    public double AverageCadence
    {
        get
        {
            double minutes = Elapsed.TotalMinutes;
            return minutes > 0 ? StepCount / minutes : 0;
        }
    }

    /// <summary>平均速度 = 距离 / 跑动时长。</summary>
    public double AverageSpeedKmh
    {
        get
        {
            double seconds = Elapsed.TotalSeconds;
            return seconds > 0 ? GeoCalculator.MetersPerSecondToKmPerHour(DistanceMeters / seconds) : 0;
        }
    }

    // 真正的快照：调用方枚举时不受传感器回调修改影响。
    public IReadOnlyList<GeoCoordinate> TrackPoints { get { lock (_lock) return _trackPoints.ToArray(); } }
    public IReadOnlyList<CadenceSample> CadenceSamples { get { lock (_lock) return _cadenceSamples.ToArray(); } }

    public void Start(int targetCadence, bool cadenceAvailable = true)
    {
        if (targetCadence <= 0) throw new ArgumentOutOfRangeException(nameof(targetCadence));
        lock (_lock)
        {
            Require(CanStart, nameof(Start));
            TargetCadence = targetCadence;
            CadenceAvailable = cadenceAvailable;
            StartTime = _clock.GetUtcNow();
            _segmentStart = StartTime.Value;
            State = RunSessionState.Running;
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            Require(CanPause, nameof(Pause));
            CloseSegment();
            CurrentCadence = 0;
            State = RunSessionState.Paused;
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            Require(CanResume, nameof(Resume));
            _segmentStart = _clock.GetUtcNow();
            State = RunSessionState.Running;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            Require(CanStop, nameof(Stop));
            if (State == RunSessionState.Running) CloseSegment();
            CurrentCadence = 0;
            EndTime = _clock.GetUtcNow();
            State = RunSessionState.Stopped;
        }
    }

    /// <summary>外部追踪器的累计距离（米）。非 Running 时忽略。</summary>
    public void UpdateDistance(double totalMeters)
    {
        if (!double.IsFinite(totalMeters) || totalMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(totalMeters));
        lock (_lock)
        {
            if (State != RunSessionState.Running) return;
            DistanceMeters = totalMeters;
        }
    }

    /// <summary>计步器的本次累计步数。非 Running 时忽略。</summary>
    public void UpdateStepCount(int totalSteps)
    {
        if (totalSteps < 0) throw new ArgumentOutOfRangeException(nameof(totalSteps));
        lock (_lock)
        {
            if (State != RunSessionState.Running) return;
            StepCount = totalSteps;
        }
    }

    /// <summary>实时步频，同时记录一条采样。非 Running 时忽略。</summary>
    public void UpdateCadence(double stepsPerMinute)
    {
        if (!double.IsFinite(stepsPerMinute) || stepsPerMinute < 0)
            throw new ArgumentOutOfRangeException(nameof(stepsPerMinute));
        lock (_lock)
        {
            if (State != RunSessionState.Running) return;
            CurrentCadence = stepsPerMinute;
            _cadenceSamples.Add(new CadenceSample(_clock.GetUtcNow(), stepsPerMinute));
        }
    }

    /// <summary>已被追踪器接受的轨迹点。非 Running 时忽略。</summary>
    public void AddTrackPoint(GeoCoordinate point)
    {
        lock (_lock)
        {
            if (State != RunSessionState.Running) return;
            _trackPoints.Add(point);
        }
    }

    private void CloseSegment() => _accumulated += _clock.GetUtcNow() - _segmentStart;

    private void Require(bool allowed, string action)
    {
        if (!allowed) throw new InvalidOperationException($"{action} 不允许：当前状态 {State}");
    }
}

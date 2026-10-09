namespace Runner.Core.Alerts;

/// <summary>
/// 步频偏离判定：|实际 − 目标| 超过容差并持续 SustainSeconds 才提醒，之后持续偏离每 CooldownSeconds 重复一次；
/// 已偏离时要回到 容差 − 回滞 的范围内才算恢复，反向偏离仍需超过完整容差；
/// 步频为 0 视为无数据，不判定并清空计时。
/// </summary>
public sealed class CadenceAlertMonitor
{
    private readonly CadenceAlertConfig _config;
    private readonly TimeProvider _clock;
    private DateTimeOffset _deviatingSince;
    private DateTimeOffset? _lastAlertTime;

    public CadenceAlertMonitor(CadenceAlertConfig? config = null, TimeProvider? clock = null)
    {
        _config = (config ?? new()).Snapshot();
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>当前偏离状态（供界面着色），与此刻是否发出提醒无关。</summary>
    public CadenceAlertKind State { get; private set; }

    public int TargetCadence => _config.TargetCadence;

    /// <summary>送入一次实时步频；返回此刻应发出的提醒，多数时候为 None。</summary>
    public CadenceAlertKind Evaluate(double stepsPerMinute)
    {
        if (!double.IsFinite(stepsPerMinute) || stepsPerMinute < 0)
            throw new ArgumentOutOfRangeException(nameof(stepsPerMinute));

        var kind = stepsPerMinute == 0 ? CadenceAlertKind.None : Classify(stepsPerMinute);
        if (kind == CadenceAlertKind.None)
        {
            Reset();
            return CadenceAlertKind.None;
        }

        var now = _clock.GetUtcNow();
        if (kind != State)
        {
            State = kind;
            _deviatingSince = now;
            _lastAlertTime = null;
        }

        if ((now - _deviatingSince).TotalSeconds < _config.SustainSeconds) return CadenceAlertKind.None;
        if (_lastAlertTime is DateTimeOffset last && (now - last).TotalSeconds < _config.CooldownSeconds)
            return CadenceAlertKind.None;

        _lastAlertTime = now;
        return kind;
    }

    public void Reset()
    {
        State = CadenceAlertKind.None;
        _lastAlertTime = null;
    }

    private CadenceAlertKind Classify(double stepsPerMinute)
    {
        double deviation = stepsPerMinute - _config.TargetCadence;
        // 同方向保持偏离用更窄的恢复门限；进入偏离（含反向）用完整容差。
        int stay = _config.ToleranceSpm - _config.HysteresisSpm;
        int fastThreshold = State == CadenceAlertKind.TooFast ? stay : _config.ToleranceSpm;
        int slowThreshold = State == CadenceAlertKind.TooSlow ? stay : _config.ToleranceSpm;
        if (deviation > fastThreshold) return CadenceAlertKind.TooFast;
        if (deviation < -slowThreshold) return CadenceAlertKind.TooSlow;
        return CadenceAlertKind.None;
    }
}

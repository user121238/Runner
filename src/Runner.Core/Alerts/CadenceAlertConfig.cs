namespace Runner.Core.Alerts;

public class CadenceAlertConfig
{
    public int TargetCadence { get; set; } = 180;
    /// <summary>允许偏差（SPM）：|实际 − 目标| 超过它才算偏离。</summary>
    public int ToleranceSpm { get; set; } = 10;
    /// <summary>回滞（SPM）：已偏离后要回到 容差 − 回滞 的范围内才算恢复，防止在阈值边缘反复触发。</summary>
    public int HysteresisSpm { get; set; } = 3;
    /// <summary>偏离须持续多少秒才提醒。</summary>
    public double SustainSeconds { get; set; } = 3;
    /// <summary>持续偏离时两次提醒的最短间隔（秒）。</summary>
    public double CooldownSeconds { get; set; } = 10;

    internal CadenceAlertConfig Snapshot()
    {
        if (TargetCadence <= 0) throw new ArgumentOutOfRangeException(nameof(TargetCadence));
        if (ToleranceSpm < 0) throw new ArgumentOutOfRangeException(nameof(ToleranceSpm));
        if (HysteresisSpm < 0 || HysteresisSpm > ToleranceSpm) throw new ArgumentOutOfRangeException(nameof(HysteresisSpm));
        ValidateNonNegative(SustainSeconds, nameof(SustainSeconds));
        ValidateNonNegative(CooldownSeconds, nameof(CooldownSeconds));
        return (CadenceAlertConfig)MemberwiseClone();
    }

    private static void ValidateNonNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name);
    }
}

namespace Runner.Business.Models;

/// <summary>用户偏好：目标步频、允许偏差与提醒开关。单条记录，Id 固定为 1。</summary>
public class RunSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public int TargetCadence { get; set; } = 180;
    public int ToleranceSpm { get; set; } = 10;
    public bool AudioAlertEnabled { get; set; } = true;
    public bool VibrationAlertEnabled { get; set; } = true;

    public RunSettings Clone() => (RunSettings)MemberwiseClone();
}

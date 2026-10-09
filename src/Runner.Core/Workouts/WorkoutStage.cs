namespace Runner.Core.Workouts;

/// <summary>课程阶段：节拍步频（0 = 不播节拍）、结束条件（时长秒或距离米，二选一）、进入时是否切下一首歌。</summary>
public class WorkoutStage
{
    public int TargetCadence { get; set; } = 180;
    public StageEndKind EndKind { get; set; } = StageEndKind.Duration;
    /// <summary>EndKind 为 Duration 时是秒，为 Distance 时是米。</summary>
    public double EndValue { get; set; } = 120;
    public bool SwitchTrackOnEnter { get; set; }

    public void Validate()
    {
        if (TargetCadence < 0 || TargetCadence > 300) throw new ArgumentOutOfRangeException(nameof(TargetCadence));
        if (!double.IsFinite(EndValue) || EndValue <= 0) throw new ArgumentOutOfRangeException(nameof(EndValue));
    }
}

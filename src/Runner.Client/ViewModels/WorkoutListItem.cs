using Runner.Core.Workouts;

namespace Runner.Client.ViewModels;

/// <summary>课程列表项：名称、一行总览与阶段明细；文案生成的静态方法也给首页摘要复用。</summary>
public sealed class WorkoutListItem
{
    public WorkoutListItem(WorkoutPlan plan)
    {
        Plan = plan;
        Name = plan.Name;
        Summary = Describe(plan);
        Stages = DescribeStages(plan);
    }

    public WorkoutPlan Plan { get; }
    public int Id => Plan.Id;
    public string Name { get; }
    public string Summary { get; }
    public string Stages { get; }

    /// <summary>"2 个阶段 × 4 轮 · 共 12:00"。</summary>
    public static string Describe(WorkoutPlan plan)
    {
        string total;
        if (plan.Stages.All(s => s.EndKind == StageEndKind.Duration))
        {
            var seconds = plan.Stages.Sum(s => s.EndValue) * plan.Rounds;
            total = $"共 {ActivityListItem.FormatDuration(seconds)}";
        }
        else if (plan.Stages.All(s => s.EndKind == StageEndKind.Distance))
        {
            var meters = plan.Stages.Sum(s => s.EndValue) * plan.Rounds;
            total = $"共 {meters / 1000:F2} 公里";
        }
        else
        {
            total = "时长与距离混合";
        }
        return $"{plan.Stages.Count} 个阶段 × {plan.Rounds} 轮 · {total}";
    }

    /// <summary>"180 SPM 2:00 · 120 SPM 1:00 切歌"。</summary>
    public static string DescribeStages(WorkoutPlan plan) => string.Join("  ·  ", plan.Stages.Select(DescribeStage));

    public static string DescribeStage(WorkoutStage stage)
    {
        string cadence = stage.TargetCadence > 0 ? $"{stage.TargetCadence} SPM" : "不播节拍";
        string end = stage.EndKind == StageEndKind.Duration
            ? ActivityListItem.FormatDuration(stage.EndValue)
            : $"{stage.EndValue:0} 米";
        return stage.SwitchTrackOnEnter ? $"{cadence} {end} 切歌" : $"{cadence} {end}";
    }
}

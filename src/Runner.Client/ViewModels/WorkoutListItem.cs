using Runner.Core.Workouts;

namespace Runner.Client.ViewModels;

/// <summary>课程列表项：名称与一行摘要。</summary>
public sealed class WorkoutListItem
{
    public WorkoutListItem(WorkoutPlan plan)
    {
        Plan = plan;
        Name = plan.Name;
        Summary = Describe(plan);
    }

    public WorkoutPlan Plan { get; }
    public int Id => Plan.Id;
    public string Name { get; }
    public string Summary { get; }

    public static string Describe(WorkoutPlan plan)
    {
        string total;
        if (plan.Stages.All(s => s.EndKind == StageEndKind.Duration))
        {
            var seconds = plan.Stages.Sum(s => s.EndValue) * plan.Rounds;
            total = $"共 {TimeSpan.FromSeconds(seconds):h\\:mm\\:ss}";
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
}

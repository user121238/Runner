namespace Runner.Core.Workouts;

/// <summary>课程：若干阶段按顺序执行，重复 Rounds 轮。Id 由存储分配，0 表示未保存。</summary>
public class WorkoutPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Rounds { get; set; } = 1;
    public List<WorkoutStage> Stages { get; set; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("课程名称不能为空", nameof(Name));
        if (Rounds < 1 || Rounds > 100) throw new ArgumentOutOfRangeException(nameof(Rounds));
        if (Stages.Count == 0) throw new ArgumentException("课程至少需要一个阶段", nameof(Stages));
        foreach (var stage in Stages) stage.Validate();
    }
}

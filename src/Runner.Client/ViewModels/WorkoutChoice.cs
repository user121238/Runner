using Prism.Mvvm;
using Runner.Core.Workouts;

namespace Runner.Client.ViewModels;

/// <summary>首页的课程芯片：Plan 为 null 表示自由跑；选中态由 RunViewModel 统一维护。</summary>
public sealed class WorkoutChoice : BindableBase
{
    private bool _isSelected;

    public WorkoutChoice(string name, WorkoutPlan? plan)
    {
        Name = name;
        Plan = plan;
    }

    public string Name { get; }
    public WorkoutPlan? Plan { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

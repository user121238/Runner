using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Runner.Business.Services.Workouts;
using Runner.Core.Workouts;

namespace Runner.Client.ViewModels;

/// <summary>课程编辑：名称、轮数、阶段列表；保存后返回列表。</summary>
public class WorkoutEditorPageViewModel : BindableBase, INavigatedAware
{
    private const int MaxRounds = 100;
    private readonly WorkoutStore _store;
    private readonly INavigationService _navigation;
    private int _planId;
    private string _name = "";
    private int _rounds = 1;
    private string _message = "";

    public WorkoutEditorPageViewModel(WorkoutStore store, INavigationService navigation)
    {
        _store = store;
        _navigation = navigation;
        IncreaseRoundsCommand = new DelegateCommand(() => Rounds = Math.Min(MaxRounds, Rounds + 1));
        DecreaseRoundsCommand = new DelegateCommand(() => Rounds = Math.Max(1, Rounds - 1));
        AddStageCommand = new DelegateCommand(() => AddStage(new StageEditModel()));
        RemoveStageCommand = new DelegateCommand<StageEditModel>(RemoveStage);
        SaveCommand = new AsyncDelegateCommand(SaveAsync);
    }

    public DelegateCommand IncreaseRoundsCommand { get; }
    public DelegateCommand DecreaseRoundsCommand { get; }
    public DelegateCommand AddStageCommand { get; }
    public DelegateCommand<StageEditModel> RemoveStageCommand { get; }
    public AsyncDelegateCommand SaveCommand { get; }

    public ObservableCollection<StageEditModel> Stages { get; } = new();

    public string Title => _planId == 0 ? "新建课程" : "编辑课程";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int Rounds
    {
        get => _rounds;
        private set => SetProperty(ref _rounds, value);
    }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) RaisePropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => _message.Length > 0;

    public void OnNavigatedFrom(INavigationParameters parameters) { }

    public void OnNavigatedTo(INavigationParameters parameters)
    {
        if (parameters.TryGetValue<int>("id", out var id) && id > 0)
            _ = LoadAsync(id);
        else
            Populate(new WorkoutPlan { Stages = { new WorkoutStage() } });
    }

    private async Task LoadAsync(int id)
    {
        try
        {
            var plan = await _store.GetAsync(id);
            if (plan == null)
            {
                Message = "课程不存在";
                return;
            }
            Populate(plan);
        }
        catch (Exception ex)
        {
            Message = $"加载失败: {ex.Message}";
        }
    }

    private void Populate(WorkoutPlan plan)
    {
        _planId = plan.Id;
        Name = plan.Name;
        Rounds = plan.Rounds;
        Stages.Clear();
        foreach (var stage in plan.Stages) AddStage(StageEditModel.From(stage));
        RaisePropertyChanged(nameof(Title));
    }

    private void AddStage(StageEditModel stage)
    {
        Stages.Add(stage);
        Renumber();
    }

    private void RemoveStage(StageEditModel stage)
    {
        Stages.Remove(stage);
        Renumber();
    }

    private void Renumber()
    {
        for (int i = 0; i < Stages.Count; i++) Stages[i].Number = i + 1;
    }

    private async Task SaveAsync()
    {
        try
        {
            var plan = new WorkoutPlan
            {
                Id = _planId,
                Name = Name.Trim(),
                Rounds = Rounds,
                Stages = Stages.Select(s => s.ToStage()).ToList()
            };
            await _store.SaveAsync(plan);
            await _navigation.GoBackAsync();
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
        }
        catch (FormatException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"保存失败: {ex.Message}";
        }
    }
}

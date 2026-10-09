using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Runner.Business.Platforms.Android.Session;
using Runner.Core.Session;
using Runner.Core.Workouts;

namespace Runner.Client.ViewModels;

/// <summary>
/// 首页：时长 / 距离 / 实际步频 / 目标步频，一个主按钮随状态变为 开始 / 暂停 / 继续；
/// 空闲时选课程（或自由跑），进行中显示课程的轮次、阶段与剩余量。
/// </summary>
public class RunViewModel : BindableBase, IRegionAware
{
    private const string FreeRunChoice = "自由跑";
    private readonly RunSessionHost _host;
    private IReadOnlyList<WorkoutPlan> _plans = Array.Empty<WorkoutPlan>();
    private bool _loadingChoices;

    private string _statusText = "正在申请权限…";
    private string _elapsed = "00:00:00";
    private double _distanceKm;
    private string _cadenceText = "0";
    private string _targetCadenceText = "—";
    private string _message = "";
    private string _primaryText = "开始";
    private bool _canStop;
    private bool _isIdle = true;
    private bool _isWorkoutActive;
    private string _workoutStatus = "";
    private int _selectedWorkoutIndex;

    public RunViewModel(RunSessionHost host)
    {
        _host = host;
        PrimaryCommand = new AsyncDelegateCommand(PrimaryAsync, () => CanPrimary);
        StopCommand = new DelegateCommand(Stop, () => CanStop);
        _host.Updated += OnHostUpdated;
        _host.Error += OnHostError;
        _host.Workouts.Changed += OnWorkoutsChanged;
    }

    public AsyncDelegateCommand PrimaryCommand { get; }
    public DelegateCommand StopCommand { get; }

    /// <summary>第 0 项固定为自由跑，之后是课程名。</summary>
    public ObservableCollection<string> WorkoutChoices { get; } = new() { FreeRunChoice };

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string Elapsed { get => _elapsed; private set => SetProperty(ref _elapsed, value); }
    public double DistanceKm { get => _distanceKm; private set => SetProperty(ref _distanceKm, value); }
    public string CadenceText { get => _cadenceText; private set => SetProperty(ref _cadenceText, value); }
    public string TargetCadenceText { get => _targetCadenceText; private set => SetProperty(ref _targetCadenceText, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string PrimaryText { get => _primaryText; private set => SetProperty(ref _primaryText, value); }
    public bool CanStop { get => _canStop; private set => SetProperty(ref _canStop, value); }
    public bool IsIdle { get => _isIdle; private set => SetProperty(ref _isIdle, value); }
    public bool IsWorkoutActive { get => _isWorkoutActive; private set => SetProperty(ref _isWorkoutActive, value); }
    public string WorkoutStatus { get => _workoutStatus; private set => SetProperty(ref _workoutStatus, value); }

    public int SelectedWorkoutIndex
    {
        get => _selectedWorkoutIndex;
        set
        {
            if (!SetProperty(ref _selectedWorkoutIndex, value) || _loadingChoices) return;
            _host.SelectedWorkout = value > 0 && value - 1 < _plans.Count ? _plans[value - 1] : null;
            Refresh();
        }
    }

    public bool CanPrimary => _host.Controller != null;

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    public void OnNavigatedTo(NavigationContext navigationContext) => _ = InitializeAsync();

    private async Task InitializeAsync()
    {
        try
        {
            await _host.InitializeAsync();
        }
        catch (Exception ex)
        {
            Message = $"初始化失败: {ex.Message}";
        }
        await LoadWorkoutChoicesAsync();
    }

    private void OnWorkoutsChanged() => MainThread.BeginInvokeOnMainThread(() => _ = LoadWorkoutChoicesAsync());

    private async Task LoadWorkoutChoicesAsync()
    {
        try
        {
            var plans = await _host.Workouts.GetAllAsync();
            int keepId = _host.SelectedWorkout?.Id ?? 0;

            _loadingChoices = true;
            _plans = plans;
            WorkoutChoices.Clear();
            WorkoutChoices.Add(FreeRunChoice);
            foreach (var plan in plans) WorkoutChoices.Add(plan.Name);
            _loadingChoices = false;

            int index = 0;
            for (int i = 0; i < plans.Count; i++)
                if (plans[i].Id == keepId) index = i + 1;

            // 被编辑过的课程要换成新实例
            _host.SelectedWorkout = index > 0 ? plans[index - 1] : null;
            SelectedWorkoutIndex = index;
        }
        catch (Exception ex)
        {
            Message = $"课程加载失败: {ex.Message}";
        }
        Refresh();
    }

    private async Task PrimaryAsync()
    {
        var controller = _host.Controller;
        if (controller == null) return;

        Message = "";
        try
        {
            switch (controller.Session?.State)
            {
                case RunSessionState.Running:
                    controller.Pause();
                    break;
                case RunSessionState.Paused:
                    await controller.ResumeAsync();
                    break;
                default:
                    await controller.StartAsync(_host.SelectedWorkout);
                    break;
            }
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
        Refresh();
    }

    private void Stop()
    {
        try
        {
            _host.Controller?.Stop();
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
        Refresh();
    }

    private void OnHostUpdated() => MainThread.BeginInvokeOnMainThread(Refresh);

    private void OnHostError(string message) => MainThread.BeginInvokeOnMainThread(() => Message = message);

    private void Refresh()
    {
        var controller = _host.Controller;
        var session = controller?.Session;
        bool cadenceAvailable = session?.CadenceAvailable ?? controller?.HasCadenceSource ?? true;
        bool active = controller?.IsActive == true;
        IsIdle = !active;

        if (session == null)
        {
            StatusText = controller == null ? "正在申请权限…"
                : controller.HasGps ? "准备就绪" : _host.LocationStatus;
            Elapsed = "00:00:00";
            DistanceKm = 0;
            CadenceText = cadenceAvailable ? "0" : "—";
            TargetCadenceText = IdleTargetText();
            PrimaryText = "开始";
            CanStop = false;
            IsWorkoutActive = false;
            WorkoutStatus = "";
        }
        else
        {
            StatusText = session.State switch
            {
                RunSessionState.Running => "跑步中",
                RunSessionState.Paused => "已暂停",
                RunSessionState.Stopped => "已结束",
                _ => "准备就绪"
            };
            Elapsed = session.Elapsed.ToString(@"hh\:mm\:ss");
            DistanceKm = session.DistanceKm;
            CadenceText = cadenceAvailable ? session.CurrentCadence.ToString("F0") : "—";
            TargetCadenceText = active
                ? (session.TargetCadence > 0 ? session.TargetCadence.ToString() : "—")
                : IdleTargetText();
            PrimaryText = session.State switch
            {
                RunSessionState.Running => "暂停",
                RunSessionState.Paused => "继续",
                _ => "开始"
            };
            CanStop = session.CanStop;

            var workout = controller!.Workout;
            IsWorkoutActive = active && workout != null;
            WorkoutStatus = IsWorkoutActive ? DescribeWorkout(workout!, session) : "";
        }

        PrimaryCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }

    private string IdleTargetText()
    {
        var first = _host.SelectedWorkout?.Stages.FirstOrDefault();
        return first is { TargetCadence: > 0 } ? first.TargetCadence.ToString() : "—";
    }

    private static string DescribeWorkout(WorkoutRunner workout, RunSession session)
    {
        if (workout.IsCompleted) return $"{workout.Name} · 课程完成";

        var stage = workout.Current!;
        double remaining = workout.Remaining(session.Elapsed, session.DistanceMeters);
        string remainingText = stage.EndKind == StageEndKind.Duration
            ? TimeSpan.FromSeconds(Math.Ceiling(remaining)).ToString(remaining >= 3600 ? @"h\:mm\:ss" : @"m\:ss")
            : $"{remaining:F0} 米";
        int rounds = workout.TotalStages / workout.StagesPerRound;
        return $"{workout.Name} · 第 {workout.CurrentRound}/{rounds} 轮 · 阶段 {workout.CurrentStageNumber}/{workout.StagesPerRound} · 剩 {remainingText}";
    }
}

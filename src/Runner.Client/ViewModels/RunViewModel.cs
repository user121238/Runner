using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using Prism.Services;
using Runner.Business.Session;
using Runner.Core.Session;
using Runner.Core.Workouts;

namespace Runner.Client.ViewModels;

/// <summary>
/// 首页三态：空闲选课程（或自由跑）看摘要；跑动中看时长 / 距离 / 步频 / 目标步频与课程阶段进度；结束后看成绩再来一次。
/// 权限与传感器来源在首次点击开始时才申请、创建，页面本身不依赖它们；结束前确认，免得误触丢掉一次跑步。
/// </summary>
public class RunViewModel : BindableBase, IRegionAware
{
    private const string FreeRunName = "自由跑";
    private static readonly CultureInfo ChineseCulture = new("zh-CN");
    private readonly RunSessionHost _host;
    private readonly IPageDialogService _dialogs;
    private readonly INavigationService _navigation;
    private WorkoutPlan? _activePlan;
    private int _lastRunId;

    private string _statusText = "";
    private string _workoutHeader = "";
    private double _stageProgress;
    private string _remainingText = "";
    private string _nextStageText = "";
    private string _elapsed = "00:00";
    private double _distanceKm;
    private string _cadenceText = "0";
    private string _targetCadenceText = "—";
    private string _paceText = "—";
    private string _averagePaceText = "—";
    private string _fastestPaceText = "—";
    private string _slowestPaceText = "—";
    private string _averageCadenceText = "0";
    private string _stepCountText = "0";
    private bool _isActive;
    private bool _isStopped;
    private string _summaryTitle = FreeRunName;
    private string _summaryDetail = "不播节拍，按自己的节奏跑";
    private string _summaryStages = "记录时长、距离与步频";
    private string _message = "";
    private string _errorDetails = "";
    private string _copyErrorText = "复制错误详情";
    private string _primaryText = "开始";
    private bool _canStop;
    private bool _hasSession;
    private bool _isIdle = true;
    private bool _isRunning;
    private bool _isPaused;
    private bool _hasWorkout;
    private bool _showIdleHeader = true;
    private bool _showNoRunHint;
    private bool _hasLastRun;
    private bool _hasLoadedLastRun;
    private string _dateTitle = "";
    private string _lastRunMetrics = "";
    private string _lastRunMeta = "";

    public RunViewModel(RunSessionHost host, IPageDialogService dialogs, INavigationService navigation)
    {
        _host = host;
        _dialogs = dialogs;
        _navigation = navigation;
        PrimaryCommand = new AsyncDelegateCommand(PrimaryAsync);
        StopCommand = new AsyncDelegateCommand(StopAsync, () => CanStop);
        SelectCommand = new DelegateCommand<WorkoutChoice>(Select);
        OpenLastRunCommand = new AsyncDelegateCommand(
            () => _navigation.NavigateAsync("ActivityDetailPage", ("id", _lastRunId)),
            () => HasLastRun);
        CopyErrorDetailsCommand = new AsyncDelegateCommand(CopyErrorDetailsAsync, () => HasErrorDetails);
        _host.Updated += OnHostUpdated;
        _host.Error += OnHostError;
        _host.Notice += OnHostNotice;
        _host.Workouts.Changed += OnWorkoutsChanged;
        _host.Activities.Changed += OnActivitiesChanged;
        _ = LoadChoicesAsync();
        _ = LoadLastRunAsync();
    }

    public AsyncDelegateCommand PrimaryCommand { get; }
    public AsyncDelegateCommand StopCommand { get; }
    public DelegateCommand<WorkoutChoice> SelectCommand { get; }
    public AsyncDelegateCommand OpenLastRunCommand { get; }
    public AsyncDelegateCommand CopyErrorDetailsCommand { get; }

    /// <summary>第 0 项固定为自由跑，之后是课程。</summary>
    public ObservableCollection<WorkoutChoice> Choices { get; } = new();

    // ---- 状态 ----
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value)) RaisePropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => _statusText.Length > 0;
    public bool HasSession { get => _hasSession; private set => SetProperty(ref _hasSession, value); }
    public bool IsIdle { get => _isIdle; private set => SetProperty(ref _isIdle, value); }
    public bool IsRunning { get => _isRunning; private set => SetProperty(ref _isRunning, value); }
    public bool IsPaused { get => _isPaused; private set => SetProperty(ref _isPaused, value); }

    // ---- 课程进度 ----
    public bool HasWorkout { get => _hasWorkout; private set => SetProperty(ref _hasWorkout, value); }
    public string WorkoutHeader { get => _workoutHeader; private set => SetProperty(ref _workoutHeader, value); }
    public double StageProgress { get => _stageProgress; private set => SetProperty(ref _stageProgress, value); }
    public string RemainingText { get => _remainingText; private set => SetProperty(ref _remainingText, value); }
    public string NextStageText { get => _nextStageText; private set => SetProperty(ref _nextStageText, value); }

    // ---- 四个数字 ----
    public string Elapsed { get => _elapsed; private set => SetProperty(ref _elapsed, value); }
    public double DistanceKm { get => _distanceKm; private set => SetProperty(ref _distanceKm, value); }
    public string CadenceText { get => _cadenceText; private set => SetProperty(ref _cadenceText, value); }
    public string TargetCadenceText { get => _targetCadenceText; private set => SetProperty(ref _targetCadenceText, value); }
    /// <summary>实时配速：最近 30 秒的 Δt/Δd；站着或 GPS 断流为 —。</summary>
    public string PaceText { get => _paceText; private set => SetProperty(ref _paceText, value); }

    // ---- 结束后的成绩 ----
    public bool IsActive { get => _isActive; private set => SetProperty(ref _isActive, value); }
    public bool IsStopped { get => _isStopped; private set => SetProperty(ref _isStopped, value); }
    public string AveragePaceText { get => _averagePaceText; private set => SetProperty(ref _averagePaceText, value); }
    public string FastestPaceText { get => _fastestPaceText; private set => SetProperty(ref _fastestPaceText, value); }
    public string SlowestPaceText { get => _slowestPaceText; private set => SetProperty(ref _slowestPaceText, value); }
    public string AverageCadenceText { get => _averageCadenceText; private set => SetProperty(ref _averageCadenceText, value); }
    public string StepCountText { get => _stepCountText; private set => SetProperty(ref _stepCountText, value); }

    // ---- 空闲时的课程摘要 ----
    public string SummaryTitle { get => _summaryTitle; private set => SetProperty(ref _summaryTitle, value); }
    public string SummaryDetail { get => _summaryDetail; private set => SetProperty(ref _summaryDetail, value); }
    public string SummaryStages { get => _summaryStages; private set => SetProperty(ref _summaryStages, value); }

    // ---- 空闲时的顶部：今天的日期与上次跑步 ----
    public bool ShowIdleHeader { get => _showIdleHeader; private set => SetProperty(ref _showIdleHeader, value); }
    public bool ShowNoRunHint { get => _showNoRunHint; private set => SetProperty(ref _showNoRunHint, value); }
    public bool HasLastRun { get => _hasLastRun; private set => SetProperty(ref _hasLastRun, value); }
    public string DateTitle { get => _dateTitle; private set => SetProperty(ref _dateTitle, value); }
    public string LastRunMetrics { get => _lastRunMetrics; private set => SetProperty(ref _lastRunMetrics, value); }
    public string LastRunMeta { get => _lastRunMeta; private set => SetProperty(ref _lastRunMeta, value); }

    // ---- 操作 ----
    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) RaisePropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => _message.Length > 0;
    public string ErrorDetails
    {
        get => _errorDetails;
        private set
        {
            if (!SetProperty(ref _errorDetails, value)) return;
            RaisePropertyChanged(nameof(HasErrorDetails));
            CopyErrorText = "复制错误详情";
            CopyErrorDetailsCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasErrorDetails => _errorDetails.Length > 0;
    public string CopyErrorText { get => _copyErrorText; private set => SetProperty(ref _copyErrorText, value); }
    public string PrimaryText { get => _primaryText; private set => SetProperty(ref _primaryText, value); }
    public bool CanStop { get => _canStop; private set => SetProperty(ref _canStop, value); }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    public void OnNavigatedTo(NavigationContext navigationContext) => Refresh();

    private void OnWorkoutsChanged() => MainThread.BeginInvokeOnMainThread(() => _ = LoadChoicesAsync());

    private void OnActivitiesChanged() => MainThread.BeginInvokeOnMainThread(() => _ = LoadLastRunAsync());

    private async Task LoadLastRunAsync()
    {
        _hasLoadedLastRun = false;
        try
        {
            var records = await _host.Activities.GetAllAsync();
            var last = records.MaxBy(r => r.StartTimeUtc);
            _lastRunId = last?.Id ?? 0;
            HasLastRun = last != null;
            if (last != null)
            {
                string workout = string.IsNullOrEmpty(last.WorkoutName) ? FreeRunName : last.WorkoutName;
                LastRunMetrics = $"{last.DistanceMeters / 1000:F2} 公里 · {ActivityListItem.FormatDuration(last.ElapsedSeconds)} · 配速 {PaceFormat.Text(PaceCalculator.Average(TimeSpan.FromSeconds(last.ElapsedSeconds), last.DistanceMeters))}";
                LastRunMeta = $"{last.StartTimeUtc.ToLocalTime():M月d日 HH:mm} · {workout}";
            }
            _hasLoadedLastRun = true;
        }
        catch (Exception ex)
        {
            SetError($"记录加载失败: {ex.Message}", ex);
        }
        OpenLastRunCommand.RaiseCanExecuteChanged();
        Refresh();
    }

    private async Task LoadChoicesAsync()
    {
        try
        {
            var plans = await _host.Workouts.GetAllAsync();
            int keepId = _host.SelectedWorkout?.Id ?? _host.PreferredWorkoutId;

            Choices.Clear();
            Choices.Add(new WorkoutChoice(FreeRunName, null));
            foreach (var plan in plans) Choices.Add(new WorkoutChoice(plan.Name, plan));

            // 被编辑过的课程换成新实例；被删掉的回到自由跑
            Select(Choices.FirstOrDefault(c => c.Plan != null && c.Plan.Id == keepId) ?? Choices[0]);
        }
        catch (Exception ex)
        {
            SetError($"课程加载失败: {ex.Message}", ex);
            Refresh();
        }
    }

    private void Select(WorkoutChoice? choice)
    {
        if (choice == null) return;
        foreach (var c in Choices) c.IsSelected = ReferenceEquals(c, choice);
        _host.SelectedWorkout = choice.Plan;
        _host.PreferredWorkoutId = choice.Plan?.Id ?? 0;

        // 文案按实际内容自然换行，卡片高度由可滚动正文计算。
        var plan = choice.Plan;
        SummaryTitle = choice.Name;
        SummaryDetail = plan == null ? "不播节拍，按自己的节奏跑" : WorkoutListItem.Describe(plan);
        SummaryStages = plan == null ? "记录时长、距离与步频" : WorkoutListItem.DescribeStages(plan);
        Refresh();
    }

    private async Task PrimaryAsync()
    {
        Message = "";
        ErrorDetails = "";
        try
        {
            // 权限与来源在首次点击开始时申请、创建：它们是运动的前提，不是页面的前提
            await _host.InitializeAsync();
            var controller = _host.Controller ?? throw new InvalidOperationException("来源未初始化");
            switch (controller.Session?.State)
            {
                case RunSessionState.Running:
                    controller.Pause();
                    break;
                case RunSessionState.Paused:
                    await controller.ResumeAsync();
                    break;
                default:
                    _activePlan = _host.SelectedWorkout;
                    await controller.StartAsync(_activePlan);
                    break;
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message, ex);
        }
        Refresh();
    }

    private async Task StopAsync()
    {
        var controller = _host.Controller;
        if (controller?.IsActive != true) return;

        bool confirmed = await _dialogs.DisplayAlertAsync("结束本次跑步？", "结束后保存记录；跑动不足 5 秒不保存。", "结束", "继续跑");
        if (!confirmed) return;
        try
        {
            controller.Stop();
        }
        catch (Exception ex)
        {
            SetError(ex.Message, ex);
        }
        Refresh();
    }

    private void OnHostUpdated() => MainThread.BeginInvokeOnMainThread(Refresh);

    private void OnHostError(string message)
    {
        // 先捕获本次详情，避免切回主线程前被下一条错误覆盖。
        string details = _host.LastErrorDetails;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Message = message;
            ErrorDetails = string.IsNullOrEmpty(details) ? message : details;
        });
    }

    private void OnHostNotice(string message) => MainThread.BeginInvokeOnMainThread(() =>
    {
        Message = message;
        ErrorDetails = "";
    });

    private void SetError(string message, Exception exception)
    {
        Message = message;
        ErrorDetails = exception.ToString();
    }

    private async Task CopyErrorDetailsAsync()
    {
        try
        {
            await Clipboard.Default.SetTextAsync(ErrorDetails);
            CopyErrorText = "已复制错误详情";
        }
        catch
        {
            CopyErrorText = "复制失败，点击重试";
        }
    }

    private void Refresh()
    {
        var controller = _host.Controller;
        var session = controller?.Session;
        var state = session?.State;
        bool active = controller?.IsActive == true;
        bool cadenceAvailable = session?.CadenceAvailable ?? controller?.HasCadenceSource ?? true;

        HasSession = session != null;
        IsIdle = !active;
        IsActive = active;
        IsStopped = state == RunSessionState.Stopped;
        ShowIdleHeader = !active && session == null;
        ShowNoRunHint = ShowIdleHeader && _hasLoadedLastRun && !HasLastRun;
        DateTitle = DateTime.Now.ToString("M月d日 dddd", ChineseCulture);
        IsRunning = state == RunSessionState.Running;
        IsPaused = state == RunSessionState.Paused;
        StatusText = state switch
        {
            RunSessionState.Running => "跑步中",
            RunSessionState.Paused => "已暂停",
            RunSessionState.Stopped => "已结束",
            _ => controller == null ? "" : (controller.HasGps ? "准备就绪" : _host.LocationStatus)
        };
        PrimaryText = state switch
        {
            RunSessionState.Running => "暂停",
            RunSessionState.Paused => "继续",
            _ => "开始"
        };
        CanStop = active && session?.CanStop == true;

        if (session == null)
        {
            Elapsed = "00:00";
            DistanceKm = 0;
            CadenceText = cadenceAvailable ? "0" : "—";
            TargetCadenceText = "—";
            PaceText = "—";
        }
        else
        {
            Elapsed = FormatElapsed(session.Elapsed);
            DistanceKm = session.DistanceKm;
            CadenceText = cadenceAvailable ? session.CurrentCadence.ToString("F0") : "—";
            TargetCadenceText = active && session.TargetCadence > 0 ? session.TargetCadence.ToString() : "—";
            PaceText = PaceFormat.Text(session.CurrentPace);
            if (IsStopped) DescribeResult(session);
        }

        var workout = controller?.Workout;
        HasWorkout = active && workout != null;
        if (HasWorkout)
        {
            DescribeWorkout(workout!, session!);
        }
        else
        {
            WorkoutHeader = workout != null && state == RunSessionState.Stopped ? workout.Name : "";
            StageProgress = 0;
            RemainingText = "";
            NextStageText = "";
        }

        StopCommand.RaiseCanExecuteChanged();
    }

    /// <summary>结束后的成绩：平均 / 最快 / 最慢配速、均步频、步数。只在 Stopped 时算，Summarize 是 O(n)。</summary>
    private void DescribeResult(RunSession session)
    {
        AveragePaceText = PaceFormat.Text(PaceCalculator.Average(session.Elapsed, session.DistanceMeters));
        var pace = PaceCalculator.Summarize(session.DistanceSamples);
        FastestPaceText = PaceFormat.Text(pace.Fastest);
        SlowestPaceText = PaceFormat.Text(pace.Slowest);
        AverageCadenceText = session.AverageCadence.ToString("F0");
        StepCountText = session.StepCount.ToString();
    }

    private void DescribeWorkout(WorkoutRunner workout, RunSession session)
    {
        int rounds = workout.TotalStages / workout.StagesPerRound;
        if (workout.IsCompleted)
        {
            WorkoutHeader = $"{workout.Name} · 课程完成";
            StageProgress = 1;
            RemainingText = "";
            NextStageText = "课程已跑完，可以继续自由跑或结束";
            return;
        }

        var stage = workout.Current!;
        double remaining = workout.Remaining(session.Elapsed, session.DistanceMeters);
        WorkoutHeader = $"{workout.Name} · 第 {workout.CurrentRound}/{rounds} 轮 · 阶段 {workout.CurrentStageNumber}/{workout.StagesPerRound}";
        StageProgress = stage.EndValue > 0 ? Math.Clamp(1 - remaining / stage.EndValue, 0, 1) : 0;
        RemainingText = stage.EndKind == StageEndKind.Duration
            ? $"剩 {ActivityListItem.FormatDuration(Math.Ceiling(remaining))}"
            : $"剩 {remaining:F0} 米";
        NextStageText = DescribeNext(workout, rounds);
    }

    /// <summary>下一阶段预告：法特莱克这种交替课程，跑的人最想知道"接下来是快还是慢"。</summary>
    private string DescribeNext(WorkoutRunner workout, int rounds)
    {
        bool lastInRound = workout.CurrentStageNumber >= workout.StagesPerRound;
        if (lastInRound && workout.CurrentRound >= rounds) return "最后一个阶段，坚持住";

        var stages = (_activePlan ?? _host.SelectedWorkout)?.Stages;
        if (stages == null || stages.Count != workout.StagesPerRound) return "";
        var next = stages[lastInRound ? 0 : workout.CurrentStageNumber];
        return "下一阶段 " + WorkoutListItem.DescribeStage(next);
    }

    /// <summary>不满 1 小时只显示 分:秒，大数字更大；满 1 小时再带小时。</summary>
    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"mm\:ss");
}

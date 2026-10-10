using Prism.Mvvm;
using Prism.Navigation;
using Runner.Business.Services.Activities;
using Runner.Client.Drawables;
using Runner.Core.Activities;
using Runner.Core.Session;

namespace Runner.Client.ViewModels;

/// <summary>记录详情：指标与轨迹草图。</summary>
public class ActivityDetailPageViewModel : BindableBase, INavigatedAware
{
    private readonly ActivityStore _store;
    private string _title = "记录";
    private string _workoutName = "";
    private string _distance = "0.00";
    private string _duration = "0:00";
    private string _averagePace = "—";
    private string _fastestPace = "—";
    private string _slowestPace = "—";
    private string _averageCadence = "0";
    private string _stepCount = "0";
    private string _footer = "";
    private string _message = "";
    private IDrawable _trajectory = new TrajectoryDrawable(Array.Empty<TrackPoint>());

    public ActivityDetailPageViewModel(ActivityStore store)
    {
        _store = store;
    }

    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string WorkoutName { get => _workoutName; private set => SetProperty(ref _workoutName, value); }
    public string Distance { get => _distance; private set => SetProperty(ref _distance, value); }
    public string Duration { get => _duration; private set => SetProperty(ref _duration, value); }
    public string AveragePace { get => _averagePace; private set => SetProperty(ref _averagePace, value); }
    public string FastestPace { get => _fastestPace; private set => SetProperty(ref _fastestPace, value); }
    public string SlowestPace { get => _slowestPace; private set => SetProperty(ref _slowestPace, value); }
    public string AverageCadence { get => _averageCadence; private set => SetProperty(ref _averageCadence, value); }
    public string StepCount { get => _stepCount; private set => SetProperty(ref _stepCount, value); }
    /// <summary>起止时间 · 均速 · 轨迹点数，一行小字。</summary>
    public string Footer { get => _footer; private set => SetProperty(ref _footer, value); }
    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) RaisePropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => _message.Length > 0;
    public IDrawable Trajectory { get => _trajectory; private set => SetProperty(ref _trajectory, value); }

    public void OnNavigatedFrom(INavigationParameters parameters) { }

    public void OnNavigatedTo(INavigationParameters parameters)
    {
        if (parameters.TryGetValue<int>("id", out var id) && id > 0)
            _ = LoadAsync(id);
        else
            Message = "没有指定记录";
    }

    private async Task LoadAsync(int id)
    {
        try
        {
            var record = await _store.GetAsync(id);
            if (record == null)
            {
                Message = "记录不存在";
                return;
            }
            Show(record);
        }
        catch (Exception ex)
        {
            Message = $"加载失败: {ex.Message}";
        }
    }

    private void Show(ActivityRecord record)
    {
        var start = record.StartTimeUtc.ToLocalTime();
        var end = record.EndTimeUtc.ToLocalTime();
        Title = start.ToString("M月d日 HH:mm");
        WorkoutName = string.IsNullOrEmpty(record.WorkoutName) ? "自由跑" : record.WorkoutName;
        Distance = (record.DistanceMeters / 1000).ToString("F2");
        Duration = ActivityListItem.FormatDuration(record.ElapsedSeconds);
        AveragePace = PaceFormat.Text(PaceCalculator.Average(TimeSpan.FromSeconds(record.ElapsedSeconds), record.DistanceMeters));
        // 旧记录没有距离序列，最快 / 最慢显示 —
        var pace = PaceCalculator.Summarize(record.DistanceSamples.Select(p => p.ToSample()).ToList());
        FastestPace = PaceFormat.Text(pace.Fastest);
        SlowestPace = PaceFormat.Text(pace.Slowest);
        AverageCadence = record.AverageCadence.ToString("F0");
        StepCount = record.StepCount.ToString();
        Footer = $"{start:yyyy-MM-dd HH:mm} – {end:HH:mm} · 均速 {record.AverageSpeedKmh:F1} km/h · 轨迹点 {record.TrackPoints.Count}";
        Trajectory = new TrajectoryDrawable(record.TrackPoints);
    }
}

using System.Globalization;
using Prism.Commands;
using Prism.Mvvm;
using Runner.Core.Workouts;

namespace Runner.Client.ViewModels;

/// <summary>课程编辑页里的一行阶段：文本输入，保存时解析校验；时长按分钟输入，距离按米。</summary>
public sealed class StageEditModel : BindableBase
{
    private static readonly Color SelectedColor = Color.FromArgb("#4CAF50");
    private static readonly Color UnselectedColor = Color.FromArgb("#333333");

    private int _number;
    private string _cadenceText = "180";
    private bool _isDuration = true;
    private string _endValueText = "2";
    private bool _switchTrackOnEnter;

    public StageEditModel()
    {
        UseDurationCommand = new DelegateCommand(() => IsDuration = true);
        UseDistanceCommand = new DelegateCommand(() => IsDuration = false);
    }

    public static StageEditModel From(WorkoutStage stage) => new()
    {
        CadenceText = stage.TargetCadence.ToString(CultureInfo.InvariantCulture),
        IsDuration = stage.EndKind == StageEndKind.Duration,
        EndValueText = stage.EndKind == StageEndKind.Duration
            ? (stage.EndValue / 60).ToString("0.##", CultureInfo.InvariantCulture)
            : stage.EndValue.ToString("0", CultureInfo.InvariantCulture),
        SwitchTrackOnEnter = stage.SwitchTrackOnEnter
    };

    public DelegateCommand UseDurationCommand { get; }
    public DelegateCommand UseDistanceCommand { get; }

    public int Number
    {
        get => _number;
        set
        {
            if (SetProperty(ref _number, value)) RaisePropertyChanged(nameof(Title));
        }
    }

    public string Title => $"阶段 {Number}";

    public string CadenceText
    {
        get => _cadenceText;
        set => SetProperty(ref _cadenceText, value);
    }

    public bool IsDuration
    {
        get => _isDuration;
        private set
        {
            if (!SetProperty(ref _isDuration, value)) return;
            RaisePropertyChanged(nameof(ValueLabel));
            RaisePropertyChanged(nameof(Unit));
            RaisePropertyChanged(nameof(DurationButtonColor));
            RaisePropertyChanged(nameof(DistanceButtonColor));
        }
    }

    public string ValueLabel => IsDuration ? "时长" : "距离";
    public string Unit => IsDuration ? "分钟" : "米";
    public Color DurationButtonColor => IsDuration ? SelectedColor : UnselectedColor;
    public Color DistanceButtonColor => IsDuration ? UnselectedColor : SelectedColor;

    public string EndValueText
    {
        get => _endValueText;
        set => SetProperty(ref _endValueText, value);
    }

    public bool SwitchTrackOnEnter
    {
        get => _switchTrackOnEnter;
        set => SetProperty(ref _switchTrackOnEnter, value);
    }

    /// <summary>解析为课程阶段；输入不合法时抛出带阶段序号的 FormatException。</summary>
    public WorkoutStage ToStage()
    {
        if (!int.TryParse(CadenceText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cadence) || cadence < 0 || cadence > 300)
            throw new FormatException($"{Title}：步频要填 0–300 的整数（0 表示不播节拍）");
        if (!double.TryParse(EndValueText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value <= 0)
            throw new FormatException($"{Title}：{ValueLabel}要大于 0");

        return new WorkoutStage
        {
            TargetCadence = cadence,
            EndKind = IsDuration ? StageEndKind.Duration : StageEndKind.Distance,
            EndValue = IsDuration ? value * 60 : value,
            SwitchTrackOnEnter = SwitchTrackOnEnter
        };
    }
}

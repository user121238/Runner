using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Runner.Business.Models;
using Runner.Business.Services.Settings;

namespace Runner.Client.ViewModels;

/// <summary>设置：目标步频、允许偏差与提醒开关，改动即保存；诊断页单独一屏，用页面跳转。</summary>
public class SettingsViewModel : BindableBase
{
    private const int CadenceStep = 5;
    private readonly INavigationService _navigationService;
    private readonly RunSettingsStore _store;
    private RunSettings _settings;
    private string _message = "";

    public SettingsViewModel(INavigationService navigationService, RunSettingsStore store)
    {
        _navigationService = navigationService;
        _store = store;
        _settings = store.Current;

        IncreaseTargetCommand = new DelegateCommand(() => Update(s => s.TargetCadence += CadenceStep));
        DecreaseTargetCommand = new DelegateCommand(() => Update(s => s.TargetCadence -= CadenceStep));
        IncreaseToleranceCommand = new DelegateCommand(() => Update(s => s.ToleranceSpm += 1));
        DecreaseToleranceCommand = new DelegateCommand(() => Update(s => s.ToleranceSpm -= 1));
        OpenDiagnosticsCommand = new AsyncDelegateCommand(() => _navigationService.NavigateAsync("DiagnosticsPage"));
    }

    public DelegateCommand IncreaseTargetCommand { get; }
    public DelegateCommand DecreaseTargetCommand { get; }
    public DelegateCommand IncreaseToleranceCommand { get; }
    public DelegateCommand DecreaseToleranceCommand { get; }
    public AsyncDelegateCommand OpenDiagnosticsCommand { get; }

    public int TargetCadence => _settings.TargetCadence;
    public int ToleranceSpm => _settings.ToleranceSpm;

    public bool AudioAlertEnabled
    {
        get => _settings.AudioAlertEnabled;
        set
        {
            if (value != _settings.AudioAlertEnabled) Update(s => s.AudioAlertEnabled = value);
        }
    }

    public bool VibrationAlertEnabled
    {
        get => _settings.VibrationAlertEnabled;
        set
        {
            if (value != _settings.VibrationAlertEnabled) Update(s => s.VibrationAlertEnabled = value);
        }
    }

    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    private void Update(Action<RunSettings> change)
    {
        var next = _settings.Clone();
        change(next);
        _settings = RunSettingsStore.Normalize(next);
        RaisePropertyChanged(nameof(TargetCadence));
        RaisePropertyChanged(nameof(ToleranceSpm));
        RaisePropertyChanged(nameof(AudioAlertEnabled));
        RaisePropertyChanged(nameof(VibrationAlertEnabled));
        _ = SaveAsync(_settings);
    }

    private async Task SaveAsync(RunSettings settings)
    {
        try
        {
            await _store.SaveAsync(settings);
            MainThread.BeginInvokeOnMainThread(() => Message = "");
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => Message = $"保存失败: {ex.Message}");
        }
    }
}

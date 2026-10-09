using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;

namespace Runner.Client.ViewModels;

/// <summary>设置：P3 填入目标步频与提醒；诊断页单独一屏，用页面跳转。</summary>
public class SettingsViewModel : BindableBase
{
    private readonly INavigationService _navigationService;

    public SettingsViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        OpenDiagnosticsCommand = new AsyncDelegateCommand(() => _navigationService.NavigateAsync("DiagnosticsPage"));
    }

    public AsyncDelegateCommand OpenDiagnosticsCommand { get; }
}

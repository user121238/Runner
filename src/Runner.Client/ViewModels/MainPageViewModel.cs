using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace Runner.Client.ViewModels;

/// <summary>根页：底栏在 MainRegion 内切换首页 / 课程 / 记录 / 设置；四个布尔给底栏画选中态。</summary>
public class MainPageViewModel : BindableBase
{
    private const string MainRegion = "MainRegion";
    private const string HomeView = "RunView";
    private readonly IRegionManager _regionManager;
    private string _activeView = HomeView;

    public MainPageViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;
        NavigateCommand = new DelegateCommand<string>(Navigate);
        // 区域一创建就显示首页
        _regionManager.RegisterViewWithRegion(MainRegion, HomeView);
    }

    public DelegateCommand<string> NavigateCommand { get; }

    public string ActiveView
    {
        get => _activeView;
        private set
        {
            if (!SetProperty(ref _activeView, value)) return;
            RaisePropertyChanged(nameof(IsRunActive));
            RaisePropertyChanged(nameof(IsWorkoutsActive));
            RaisePropertyChanged(nameof(IsHistoryActive));
            RaisePropertyChanged(nameof(IsSettingsActive));
        }
    }

    public bool IsRunActive => ActiveView == HomeView;
    public bool IsWorkoutsActive => ActiveView == "WorkoutsView";
    public bool IsHistoryActive => ActiveView == "HistoryView";
    public bool IsSettingsActive => ActiveView == "SettingsView";

    private void Navigate(string viewName)
    {
        if (string.IsNullOrEmpty(viewName) || viewName == ActiveView) return;
        _regionManager.RequestNavigate(MainRegion, viewName, result =>
        {
            if (result.Success) ActiveView = viewName;
        });
    }
}

using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace Runner.Client.ViewModels;

/// <summary>根页：底栏在 MainRegion 内切换首页 / 记录 / 设置。</summary>
public class MainPageViewModel : BindableBase
{
    private const string MainRegion = "MainRegion";
    private readonly IRegionManager _regionManager;
    private string _activeView = "RunView";

    public MainPageViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;
        NavigateCommand = new DelegateCommand<string>(Navigate);
        // 区域一创建就显示首页
        _regionManager.RegisterViewWithRegion(MainRegion, "RunView");
    }

    public DelegateCommand<string> NavigateCommand { get; }

    public string ActiveView
    {
        get => _activeView;
        private set => SetProperty(ref _activeView, value);
    }

    private void Navigate(string viewName)
    {
        if (string.IsNullOrEmpty(viewName) || viewName == ActiveView) return;
        _regionManager.RequestNavigate(MainRegion, viewName, result =>
        {
            if (result.Success) ActiveView = viewName;
        });
    }
}

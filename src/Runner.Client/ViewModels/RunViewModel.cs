using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Runner.Business.Models;
using Runner.Business.Platforms.Android.Session;
using Runner.Core.Alerts;
using Runner.Core.Session;

namespace Runner.Client.ViewModels;

/// <summary>
/// 首页：时长 / 距离 / 实际步频 / 目标步频，一个主按钮随状态变为 开始 / 暂停 / 继续；
/// 实际步频按偏离状态着色（过慢橙、过快红）。
/// </summary>
public class RunViewModel : BindableBase, IRegionAware
{
    private static readonly Color NormalColor = Colors.White;
    private static readonly Color TooSlowColor = Color.FromArgb("#FFB300");
    private static readonly Color TooFastColor = Color.FromArgb("#FF5252");

    private readonly RunSessionHost _host;

    private string _statusText = "正在申请权限…";
    private string _elapsed = "00:00:00";
    private double _distanceKm;
    private string _cadenceText = "0";
    private Color _cadenceColor = NormalColor;
    private int _targetCadence;
    private string _message = "";
    private string _primaryText = "开始";
    private bool _canStop;

    public RunViewModel(RunSessionHost host)
    {
        _host = host;
        _targetCadence = _host.Settings.Current.TargetCadence;
        PrimaryCommand = new AsyncDelegateCommand(PrimaryAsync, () => CanPrimary);
        StopCommand = new DelegateCommand(Stop, () => CanStop);
        _host.Updated += OnHostUpdated;
        _host.Error += OnHostError;
        _host.Settings.Changed += OnSettingsChanged;
    }

    public AsyncDelegateCommand PrimaryCommand { get; }
    public DelegateCommand StopCommand { get; }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string Elapsed { get => _elapsed; private set => SetProperty(ref _elapsed, value); }
    public double DistanceKm { get => _distanceKm; private set => SetProperty(ref _distanceKm, value); }
    public string CadenceText { get => _cadenceText; private set => SetProperty(ref _cadenceText, value); }
    public Color CadenceColor { get => _cadenceColor; private set => SetProperty(ref _cadenceColor, value); }
    public int TargetCadence { get => _targetCadence; private set => SetProperty(ref _targetCadence, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string PrimaryText { get => _primaryText; private set => SetProperty(ref _primaryText, value); }
    public bool CanStop { get => _canStop; private set => SetProperty(ref _canStop, value); }

    public bool CanPrimary => _host.Controller != null;

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    public void OnNavigatedTo(NavigationContext navigationContext) => _ = EnsureInitializedAsync();

    private async Task EnsureInitializedAsync()
    {
        try
        {
            await _host.InitializeAsync();
        }
        catch (Exception ex)
        {
            Message = $"初始化失败: {ex.Message}";
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
                    await controller.StartAsync(_host.Settings.Current);
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

    private void OnSettingsChanged(RunSettings settings) => MainThread.BeginInvokeOnMainThread(Refresh);

    private void Refresh()
    {
        var controller = _host.Controller;
        var session = controller?.Session;
        bool cadenceAvailable = session?.CadenceAvailable ?? controller?.HasCadenceSource ?? true;

        if (session == null)
        {
            StatusText = controller == null ? "正在申请权限…"
                : controller.HasGps ? "准备就绪" : _host.LocationStatus;
            Elapsed = "00:00:00";
            DistanceKm = 0;
            CadenceText = cadenceAvailable ? "0" : "—";
            CadenceColor = NormalColor;
            TargetCadence = _host.Settings.Current.TargetCadence;
            PrimaryText = "开始";
            CanStop = false;
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
            CadenceColor = controller!.AlertState switch
            {
                CadenceAlertKind.TooSlow => TooSlowColor,
                CadenceAlertKind.TooFast => TooFastColor,
                _ => NormalColor
            };
            TargetCadence = session.State == RunSessionState.Stopped
                ? _host.Settings.Current.TargetCadence
                : session.TargetCadence;
            PrimaryText = session.State switch
            {
                RunSessionState.Running => "暂停",
                RunSessionState.Paused => "继续",
                _ => "开始"
            };
            CanStop = session.CanStop;
        }

        PrimaryCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }
}

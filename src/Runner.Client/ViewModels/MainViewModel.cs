using CommunityToolkit.Mvvm.Input;
using DM.Maui.Framework.Abstractions.Navigation;
using DM.Maui.Framework.Core.Mvvm;
using Microsoft.Extensions.Logging;
using Runner.Client.Platforms.Android.Native;

namespace Runner.Client.ViewModels;

/// <summary>
/// 主页 ViewModel - 展示框架核心功能
/// </summary>
public partial class MainViewModel : BaseViewModel, IDisposable
{
    private readonly CadencePlay _cadencePlay;

    public MainViewModel(ILogger<MainViewModel> logger, INavigationService navigation) : base(logger, navigation)
    {
        _cadencePlay = new CadencePlay();
        _cadencePlay.Init();
    }

    [RelayCommand]
    private void NextTrack()
    {
        MediaController.NextTrack();
    }

    [RelayCommand]
    private void PreviousTrack()
    {
        MediaController.PreviousTrack();
    }

    [RelayCommand]
    private void PlayPause()
    {
        MediaController.PlayPause();
    }

    [RelayCommand]
    private void StartTick()
    {
        _cadencePlay.Start(180);
    }

    [RelayCommand]
    private void StopTick()
    {
        _cadencePlay.Stop();
    }

    public void Dispose()
    {
        _cadencePlay?.Dispose();
    }
}

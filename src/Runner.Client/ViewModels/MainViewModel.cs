using CommunityToolkit.Mvvm.Input;
using DM.Maui.Framework.Abstractions.Navigation;
using DM.Maui.Framework.Core.Mvvm;
using Microsoft.Extensions.Logging;

namespace Runner.Client.ViewModels;

/// <summary>
/// 主页 ViewModel - 展示框架核心功能
/// </summary>
public partial class MainViewModel : BaseViewModel
{
    public MainViewModel(ILogger<MainViewModel> logger, INavigationService navigation) : base(logger, navigation)
    {
    }


    [RelayCommand]
    private void NextTrack()
    {
        Platforms.Android.Native.MediaController.NextTrack();
    }

    [RelayCommand]
    private void PreviousTrack()
    {
        Platforms.Android.Native.MediaController.PreviousTrack();
    }

    [RelayCommand]
    private void PlayPause()
    {
        Platforms.Android.Native.MediaController.PlayPause();
    }

}

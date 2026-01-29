using DM.Maui.Framework.Core.Mvvm;
using Runner.Client.ViewModels;
using Microsoft.Maui.Controls;
using Application = Android.App.Application;

namespace Runner.Client.Views;

public partial class MainPage : BaseContentPage
{
    public MainPage(MainViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
        
        // 请求必要的传感器权限
        RequestPermissionsAsync();
    }
    
    private async void RequestPermissionsAsync()
    {
        // 在Android平台上请求身体传感器权限
        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
        {
            var status = await Permissions.RequestAsync<Permissions.Sensors>();
            
            if (status != PermissionStatus.Granted)
            {
                // 权限被拒绝，显示警告
                await DisplayAlert("权限", "需要传感器权限来使用计步器功能", "确定");
            }
        }
    }
}

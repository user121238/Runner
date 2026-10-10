using Android.Content;
using Android.OS;
using Runner.Business.Location;
using Runner.Business.Media;
using Runner.Business.Platforms.Android.Location;
using Runner.Business.Platforms.Android.Media;
using Runner.Business.Platforms.Android.Permissions;
using Runner.Business.Platforms.Android.Services;
using Runner.Business.Platforms.Android.StepCounter;
using Runner.Business.Session;
using Runner.Business.StepCounter;
using Runner.Core.SignalProcessing;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;

namespace Runner.Business.Platforms.Android.Session;

public sealed class AndroidRunPlatform(int tickSoundResourceId) : IRunPlatform
{
    public TimeProvider Clock { get; } = new SensorClock(SystemClock.ElapsedRealtimeNanos);
    public bool CanSwitchStepCounter => true;
    public bool CanControlMusic => true;
    public string MusicControlStatus => "向当前媒体应用发送播放控制命令";

    public ICadencePlayer CreateCadencePlayer() => new CadencePlay(tickSoundResourceId);

    public async Task<(IStepCounter? Counter, string Status)> CreateStepCounterAsync(bool forceAccelerometer)
    {
        if (!forceAccelerometer)
        {
            var status = await MauiPermissions.CheckStatusAsync<ActivityRecognitionPermission>();
            if (status != PermissionStatus.Granted)
                status = await MauiPermissions.RequestAsync<ActivityRecognitionPermission>();
            if (status != PermissionStatus.Granted) return (null, "需要活动识别权限");
        }

        StepCounterFactory.ForceAccelerometer = forceAccelerometer;
        var counter = StepCounterFactory.Create(Platform.AppContext);
        return (counter, counter switch
        {
            null => "设备不支持计步，仅节拍器模式",
            HardwareStepCounter => "硬件传感器",
            _ => "加速度传感器"
        });
    }

    public async Task<(ILocationTracker? Tracker, string Status)> CreateLocationTrackerAsync()
    {
        var status = await MauiPermissions.CheckStatusAsync<LocationPermission>();
        if (status != PermissionStatus.Granted)
            status = await MauiPermissions.RequestAsync<LocationPermission>();
        return status == PermissionStatus.Granted
            ? (new AndroidLocationTracker(Platform.AppContext), "已就绪")
            : (null, "需要位置权限");
    }

    public async Task PrepareAsync()
    {
        if (await MauiPermissions.CheckStatusAsync<MauiPermissions.PostNotifications>() != PermissionStatus.Granted)
            await MauiPermissions.RequestAsync<MauiPermissions.PostNotifications>();
    }

    public void StartBackgroundSession(bool hasLocation)
    {
        var intent = new Intent(Platform.AppContext, typeof(RunnerService));
        intent.PutExtra(RunnerService.UseLocationExtra, hasLocation);
        Platform.AppContext.StartForegroundService(intent);
    }

    public void StopBackgroundSession() =>
        Platform.AppContext.StopService(new Intent(Platform.AppContext, typeof(RunnerService)));

    public void NextTrack() => MediaController.NextTrack(Platform.AppContext);
    public void PreviousTrack() => MediaController.PreviousTrack(Platform.AppContext);
    public void PlayPause() => MediaController.PlayPause(Platform.AppContext);
}

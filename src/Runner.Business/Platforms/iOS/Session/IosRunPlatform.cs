using CoreMotion;
using Runner.Business.Location;
using Runner.Business.Media;
using Runner.Business.Platforms.iOS.Location;
using Runner.Business.Platforms.iOS.Media;
using Runner.Business.Platforms.iOS.StepCounter;
using Runner.Business.Session;
using Runner.Business.StepCounter;
using Runner.Core.SignalProcessing;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;

namespace Runner.Business.Platforms.iOS.Session;

public sealed class IosRunPlatform : IRunPlatform
{
    public TimeProvider Clock { get; } = new SensorClock(IosMonotonicClock.GetNanoseconds);
    public bool CanSwitchStepCounter => false;
    // MediaRemote 是私有框架：加载失败就如实报不可用，而不是让按钮出现后再抛异常。
    public bool CanControlMusic => MediaRemoteController.TryLoad();
    public string MusicControlStatus => MediaRemoteController.TryLoad()
        ? "通过 MediaRemote 向当前媒体应用发送播放控制命令"
        : $"MediaRemote 不可用: {MediaRemoteController.LoadError}";

    public ICadencePlayer CreateCadencePlayer() => new IosCadencePlayer("beep3.wav");

    public async Task<(IStepCounter? Counter, string Status)> CreateStepCounterAsync(bool forceAccelerometer)
    {
        if (!CMPedometer.IsStepCountingAvailable) return (null, "设备不支持系统计步，仅节拍器模式");
        if (!await IosStepCounter.RequestPermissionAsync()) return (null, "需要运动与健身权限（设置 → Runner → 运动与健身）");
        return (new IosStepCounter(Clock), "iOS 系统计步");
    }

    public async Task<(ILocationTracker? Tracker, string Status)> CreateLocationTrackerAsync()
    {
        var status = await MauiPermissions.CheckStatusAsync<MauiPermissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            status = await MauiPermissions.RequestAsync<MauiPermissions.LocationWhenInUse>();
        return status == PermissionStatus.Granted
            ? (new IosLocationTracker(Clock), "已就绪")
            : (null, status == PermissionStatus.Denied ? "位置权限被拒绝（设置 → Runner → 位置）" : "需要位置权限");
    }

    public Task PrepareAsync() => Task.CompletedTask;

    // iOS 没有 Android 式前台服务。CLLocationManager 和 AVAudioSession 在来源 Start/Stop
    // 中分别管理 location/audio 后台模式；不播放静音音频来伪造后台保活。
    public void StartBackgroundSession(bool hasLocation) { }
    public void StopBackgroundSession() { }

    public void NextTrack() => MediaRemoteController.Send(MediaRemoteCommand.NextTrack);
    public void PreviousTrack() => MediaRemoteController.Send(MediaRemoteCommand.PreviousTrack);
    public void PlayPause() => MediaRemoteController.Send(MediaRemoteCommand.TogglePlayPause);
}

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
    public bool CanControlMusic => false;
    public string MusicControlStatus => "iOS 不支持控制其他音乐应用；请使用系统播放控件。节拍器可与音乐混音。";

    public ICadencePlayer CreateCadencePlayer() => new IosCadencePlayer("beep3.wav");

    public async Task<(IStepCounter? Counter, string Status)> CreateStepCounterAsync(bool forceAccelerometer)
    {
        if (!CMPedometer.IsStepCountingAvailable) return (null, "设备不支持系统计步，仅节拍器模式");
        if (!await IosStepCounter.RequestPermissionAsync()) return (null, "需要运动与健身权限");
        return (new IosStepCounter(Clock), "iOS 系统计步");
    }

    public async Task<(ILocationTracker? Tracker, string Status)> CreateLocationTrackerAsync()
    {
        var status = await MauiPermissions.CheckStatusAsync<MauiPermissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            status = await MauiPermissions.RequestAsync<MauiPermissions.LocationWhenInUse>();
        return status == PermissionStatus.Granted
            ? (new IosLocationTracker(Clock), "已就绪")
            : (null, "需要位置权限");
    }

    public Task PrepareAsync() => Task.CompletedTask;

    // iOS 没有 Android 式前台服务。CLLocationManager 和 AVAudioSession 在来源 Start/Stop
    // 中分别管理 location/audio 后台模式；不播放静音音频来伪造后台保活。
    public void StartBackgroundSession(bool hasLocation) { }
    public void StopBackgroundSession() { }

    public void NextTrack() => throw new NotSupportedException(MusicControlStatus);
    public void PreviousTrack() => throw new NotSupportedException(MusicControlStatus);
    public void PlayPause() => throw new NotSupportedException(MusicControlStatus);
}

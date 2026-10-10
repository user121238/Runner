using Runner.Business.Location;
using Runner.Business.Media;
using Runner.Business.StepCounter;

namespace Runner.Business.Session;

/// <summary>平台边界：原生权限、运动来源、后台执行和系统音乐控制，不包含会话业务逻辑。</summary>
public interface IRunPlatform
{
    TimeProvider Clock { get; }
    bool CanSwitchStepCounter { get; }
    bool CanControlMusic { get; }
    string MusicControlStatus { get; }
    ICadencePlayer CreateCadencePlayer();
    Task<(IStepCounter? Counter, string Status)> CreateStepCounterAsync(bool forceAccelerometer);
    Task<(ILocationTracker? Tracker, string Status)> CreateLocationTrackerAsync();
    Task PrepareAsync();
    void StartBackgroundSession(bool hasLocation);
    void StopBackgroundSession();
    void NextTrack();
    void PreviousTrack();
    void PlayPause();
}

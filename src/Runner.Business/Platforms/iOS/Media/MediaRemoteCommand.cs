namespace Runner.Business.Platforms.iOS.Media;

/// <summary>MediaRemote 私有框架的 MRMediaRemoteCommand 编号；0 = Play、1 = Pause、3 = Stop 未用到，不列。</summary>
public enum MediaRemoteCommand
{
    TogglePlayPause = 2,
    NextTrack = 4,
    PreviousTrack = 5,
}

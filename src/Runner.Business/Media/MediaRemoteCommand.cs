namespace Runner.Business.Media;

/// <summary>MediaRemote 私有框架的 MRMediaRemoteCommand 编号，仅用于 iOS 真机测试。</summary>
public enum MediaRemoteCommand
{
    Play = 0,
    Pause = 1,
    // 2 = TogglePlayPause、3 = Stop，接入正式功能时按需再补
    NextTrack = 4,
    PreviousTrack = 5,
}

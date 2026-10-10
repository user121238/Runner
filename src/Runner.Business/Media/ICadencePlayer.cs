namespace Runner.Business.Media;

/// <summary>节拍音播放器；播放实现负责在平台允许的后台音频会话中维持节奏。</summary>
public interface ICadencePlayer : IDisposable
{
    event Action<string>? OnError;
    Task InitializeAsync();
    void Start(int stepsPerMinute);
    void Stop();
}

namespace Runner.Business.StepCounter;

/// <summary>一次运动的累计步数与实时步频；Stop/Start 分段，Reset 开始新的计数。</summary>
public interface IStepCounter : IDisposable
{
    event Action<int>? OnStepCountChanged;
    event Action<double>? OnStepFrequencyChanged;
    event Action<string>? OnError;
    void Start();
    void Stop();
    void Reset();
}

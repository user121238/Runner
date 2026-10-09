namespace Runner.Core.SignalProcessing;

/// <summary>运动内累计步数；硬件开机累计值只用于计算增量，不包含暂停期间步数。</summary>
public sealed class StepCountAccumulator
{
    private int? _lastHardwareTotal;
    public int Count { get; private set; }

    public void AddStep() => Count++;

    public void UpdateHardwareTotal(int total)
    {
        if (total < 0) return;
        if (_lastHardwareTotal is int last && total >= last)
            Count += total - last;
        _lastHardwareTotal = total;
    }

    public void Reset() => Count = 0;

    public void BeginSegment() => _lastHardwareTotal = null;
}

namespace Runner.Core.Session;

/// <summary>某一时刻的实际步频采样，供图表与记录使用。</summary>
public readonly record struct CadenceSample(DateTimeOffset Time, double StepsPerMinute);

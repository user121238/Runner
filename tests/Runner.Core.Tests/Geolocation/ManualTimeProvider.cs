namespace Runner.Core.Tests.Geolocation;

internal sealed class ManualTimeProvider : TimeProvider
{
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private DateTimeOffset _now = Epoch;

    public override DateTimeOffset GetUtcNow() => _now;

    public void SetSeconds(double seconds) => _now = Epoch.AddSeconds(seconds);
}

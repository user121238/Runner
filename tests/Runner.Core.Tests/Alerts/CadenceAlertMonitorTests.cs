using Runner.Core.Alerts;
using Runner.Core.Tests.Geolocation;

namespace Runner.Core.Tests.Alerts;

[TestClass]
public sealed class CadenceAlertMonitorTests
{
    private readonly ManualTimeProvider _clock = new();

    private CadenceAlertMonitor CreateMonitor(int tolerance = 10, int hysteresis = 3, double sustain = 3, double cooldown = 10) =>
        new(new CadenceAlertConfig
        {
            TargetCadence = 180,
            ToleranceSpm = tolerance,
            HysteresisSpm = hysteresis,
            SustainSeconds = sustain,
            CooldownSeconds = cooldown
        }, _clock);

    private CadenceAlertKind At(CadenceAlertMonitor monitor, double seconds, double cadence)
    {
        _clock.SetSeconds(seconds);
        return monitor.Evaluate(cadence);
    }

    [TestMethod]
    public void NewMonitor_WithinTolerance_NoAlertNoState()
    {
        var monitor = CreateMonitor();

        Assert.AreEqual(CadenceAlertKind.None, monitor.State);
        Assert.AreEqual(180, monitor.TargetCadence);
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 0, 180));
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 1, 190));
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 2, 170));
        Assert.AreEqual(CadenceAlertKind.None, monitor.State);
    }

    [TestMethod]
    public void Deviation_AlertsOnlyAfterSustainSeconds()
    {
        var monitor = CreateMonitor();

        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 0, 160));
        Assert.AreEqual(CadenceAlertKind.TooSlow, monitor.State);
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 2.999, 160));
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 3, 160));
    }

    [TestMethod]
    public void TooFast_IsSymmetric()
    {
        var monitor = CreateMonitor();

        At(monitor, 0, 200);
        Assert.AreEqual(CadenceAlertKind.TooFast, monitor.State);
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 2.999, 200));
        Assert.AreEqual(CadenceAlertKind.TooFast, At(monitor, 3, 200));
    }

    [TestMethod]
    public void Alert_RepeatsAfterCooldownWhileDeviating()
    {
        var monitor = CreateMonitor();
        At(monitor, 0, 160);

        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 3, 160));
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 5, 160));
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 12.999, 160));
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 13, 160));
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 20, 160));
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 23, 160));
    }

    [TestMethod]
    public void Recovery_ClearsStateAndRestartsSustain()
    {
        var monitor = CreateMonitor();
        At(monitor, 0, 160);
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 3, 160));

        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 4, 180));
        Assert.AreEqual(CadenceAlertKind.None, monitor.State);

        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 5, 160));
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 7.999, 160));
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 8, 160));
    }

    [TestMethod]
    public void Hysteresis_StaysDeviatingInsideBand()
    {
        var monitor = CreateMonitor(tolerance: 10, hysteresis: 3);
        At(monitor, 0, 165);
        Assert.AreEqual(CadenceAlertKind.TooSlow, monitor.State);

        // 偏差 -8，恢复门限是 7，仍视为偏离，持续计时不中断
        At(monitor, 1, 172);
        Assert.AreEqual(CadenceAlertKind.TooSlow, monitor.State);
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 3, 172));

        // 偏差 -6，回到恢复门限内
        At(monitor, 4, 174);
        Assert.AreEqual(CadenceAlertKind.None, monitor.State);
    }

    [TestMethod]
    public void Hysteresis_OppositeDirectionStillNeedsFullTolerance()
    {
        var monitor = CreateMonitor(tolerance: 10, hysteresis: 3);
        At(monitor, 0, 165);

        // +8 没有超过完整容差 10，不算过快，视为恢复
        At(monitor, 1, 188);
        Assert.AreEqual(CadenceAlertKind.None, monitor.State);

        At(monitor, 2, 191);
        Assert.AreEqual(CadenceAlertKind.TooFast, monitor.State);
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 4.999, 191));
        Assert.AreEqual(CadenceAlertKind.TooFast, At(monitor, 5, 191));
    }

    [TestMethod]
    public void DirectionChange_RestartsSustainAndCooldown()
    {
        var monitor = CreateMonitor();
        At(monitor, 0, 160);
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 3, 160));

        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 4, 200));
        Assert.AreEqual(CadenceAlertKind.TooFast, monitor.State);
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 6.999, 200));
        Assert.AreEqual(CadenceAlertKind.TooFast, At(monitor, 7, 200));
    }

    [TestMethod]
    public void ZeroCadence_IsUnknownAndResets()
    {
        var monitor = CreateMonitor();
        At(monitor, 0, 160);

        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 2, 0));
        Assert.AreEqual(CadenceAlertKind.None, monitor.State);

        At(monitor, 3, 160);
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 5.999, 160));
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 6, 160));
    }

    [TestMethod]
    public void ZeroSustainAndCooldown_AlertsEverySample()
    {
        var monitor = CreateMonitor(sustain: 0, cooldown: 0);

        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 0, 160));
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 1, 160));
    }

    [TestMethod]
    public void Reset_ClearsStateAndTiming()
    {
        var monitor = CreateMonitor();
        At(monitor, 0, 160);
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 3, 160));

        monitor.Reset();

        Assert.AreEqual(CadenceAlertKind.None, monitor.State);
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 4, 160));
        Assert.AreEqual(CadenceAlertKind.TooSlow, At(monitor, 7, 160));
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(-1.0)]
    public void Evaluate_InvalidCadence_Throws(double cadence)
    {
        var monitor = CreateMonitor();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => monitor.Evaluate(cadence));
    }

    [TestMethod]
    public void Config_IsSnapshotAtConstruction()
    {
        var config = new CadenceAlertConfig { TargetCadence = 180, ToleranceSpm = 10 };
        var monitor = new CadenceAlertMonitor(config, _clock);

        config.TargetCadence = 120;

        Assert.AreEqual(180, monitor.TargetCadence);
        Assert.AreEqual(CadenceAlertKind.None, At(monitor, 0, 180));
        Assert.AreEqual(CadenceAlertKind.None, monitor.State);
    }
}

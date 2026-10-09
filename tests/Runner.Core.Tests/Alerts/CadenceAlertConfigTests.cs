using Runner.Core.Alerts;

namespace Runner.Core.Tests.Alerts;

[TestClass]
public sealed class CadenceAlertConfigTests
{
    [TestMethod]
    public void Defaults_AreUsable()
    {
        var config = new CadenceAlertConfig();

        Assert.AreEqual(180, config.TargetCadence);
        Assert.AreEqual(10, config.ToleranceSpm);
        Assert.AreEqual(3, config.HysteresisSpm);
        Assert.AreEqual(3, config.SustainSeconds);
        Assert.AreEqual(10, config.CooldownSeconds);
        Assert.AreEqual(180, new CadenceAlertMonitor(config).TargetCadence);
    }

    [TestMethod]
    public void InvalidValues_Throw()
    {
        AssertInvalid(c => c.TargetCadence = 0);
        AssertInvalid(c => c.TargetCadence = -1);
        AssertInvalid(c => c.ToleranceSpm = -1);
        AssertInvalid(c => c.HysteresisSpm = -1);
        AssertInvalid(c => c.HysteresisSpm = c.ToleranceSpm + 1);
        AssertInvalid(c => c.SustainSeconds = -1);
        AssertInvalid(c => c.SustainSeconds = double.NaN);
        AssertInvalid(c => c.CooldownSeconds = -1);
        AssertInvalid(c => c.CooldownSeconds = double.PositiveInfinity);
    }

    [TestMethod]
    public void HysteresisEqualToTolerance_IsAllowed()
    {
        var config = new CadenceAlertConfig { ToleranceSpm = 5, HysteresisSpm = 5 };

        Assert.AreEqual(180, new CadenceAlertMonitor(config).TargetCadence);
    }

    private static void AssertInvalid(Action<CadenceAlertConfig> mutate)
    {
        var config = new CadenceAlertConfig();
        mutate(config);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CadenceAlertMonitor(config));
    }
}

using Runner.Core.SignalProcessing;

namespace Runner.Core.Tests.SignalProcessing;

[TestClass]
public sealed class LowPassFilterTests
{
    [TestMethod]
    [DataRow(0f)]
    [DataRow(-0.1f)]
    [DataRow(1.01f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Constructor_InvalidAlpha_RejectsValue(float alpha)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LowPassFilter(alpha));

        Assert.AreEqual("alpha", exception.ParamName);
    }

    [TestMethod]
    public void Filter_AlphaOne_PassesInputThrough()
    {
        var filter = new LowPassFilter(1);

        Assert.AreEqual(8, filter.Filter(8));
        Assert.AreEqual(-2, filter.Filter(-2));
    }

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void Filter_NonFiniteInput_ThrowsWithoutPoisoningState(float value)
    {
        var filter = new LowPassFilter(0.25f);
        filter.Filter(8);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => filter.Filter(value));

        Assert.AreEqual(3.5f, filter.Filter(8));
    }

    [TestMethod]
    public void Filter_OneQuarterAlpha_FollowsWeightedSequence()
    {
        var filter = new LowPassFilter(0.25f);

        Assert.AreEqual(2, filter.Filter(8));
        Assert.AreEqual(3.5f, filter.Filter(8));
        Assert.AreEqual(2.625f, filter.Filter(0));
    }

    [TestMethod]
    public void Reset_PreviousSamples_RestoresInitialFilterState()
    {
        var filter = new LowPassFilter(0.25f);
        filter.Filter(8);
        filter.Filter(8);

        filter.Reset();

        Assert.AreEqual(2, filter.Filter(8));
    }
}

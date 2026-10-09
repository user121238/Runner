using Runner.Core.SignalProcessing;

namespace Runner.Core.Tests.SignalProcessing;

[TestClass]
public sealed class StepCountAccumulatorTests
{
    [TestMethod]
    public void UpdateHardwareTotal_NegativeValue_DoesNotCorruptHardwareBaseline()
    {
        var accumulator = new StepCountAccumulator();
        accumulator.UpdateHardwareTotal(100);
        accumulator.UpdateHardwareTotal(102);

        accumulator.UpdateHardwareTotal(-1);
        accumulator.UpdateHardwareTotal(103);

        Assert.AreEqual(3, accumulator.Count);
    }

    [TestMethod]
    public void AddStep_DetectorEvents_CountsEachStep()
    {
        var accumulator = new StepCountAccumulator();
        Assert.AreEqual(0, accumulator.Count);

        accumulator.AddStep();
        accumulator.AddStep();

        Assert.AreEqual(2, accumulator.Count);
    }

    [TestMethod]
    public void UpdateHardwareTotal_FirstCumulativeValue_OnlyEstablishesBaseline()
    {
        var accumulator = new StepCountAccumulator();

        accumulator.UpdateHardwareTotal(10000);

        Assert.AreEqual(0, accumulator.Count);
        accumulator.UpdateHardwareTotal(10004);
        Assert.AreEqual(4, accumulator.Count);
    }

    [TestMethod]
    public void UpdateHardwareTotal_RepeatedValue_DoesNotDoubleCount()
    {
        var accumulator = new StepCountAccumulator();
        accumulator.UpdateHardwareTotal(100);
        accumulator.UpdateHardwareTotal(102);

        accumulator.UpdateHardwareTotal(102);

        Assert.AreEqual(2, accumulator.Count);
    }

    [TestMethod]
    public void UpdateHardwareTotal_HardwareCounterResets_StartsNewBaselineWithoutNegativeCount()
    {
        var accumulator = new StepCountAccumulator();
        accumulator.UpdateHardwareTotal(100);
        accumulator.UpdateHardwareTotal(103);

        accumulator.UpdateHardwareTotal(0);
        Assert.AreEqual(3, accumulator.Count);
        accumulator.UpdateHardwareTotal(2);

        Assert.AreEqual(5, accumulator.Count);
    }

    [TestMethod]
    public void Reset_HardwareCounterKeepsRunning_NextStepStartsAtOne()
    {
        var accumulator = new StepCountAccumulator();
        accumulator.UpdateHardwareTotal(1000);
        accumulator.UpdateHardwareTotal(1100);
        Assert.AreEqual(100, accumulator.Count);

        accumulator.Reset();

        Assert.AreEqual(0, accumulator.Count);
        accumulator.UpdateHardwareTotal(1101);
        Assert.AreEqual(1, accumulator.Count);
    }

    [TestMethod]
    public void Reset_DetectorEvents_NextStepStartsAtOne()
    {
        var accumulator = new StepCountAccumulator();
        accumulator.AddStep();
        accumulator.AddStep();

        accumulator.Reset();
        accumulator.AddStep();

        Assert.AreEqual(1, accumulator.Count);
    }

    [TestMethod]
    public void BeginSegment_StepsWhilePaused_AreExcludedFromResumedSession()
    {
        var accumulator = new StepCountAccumulator();
        accumulator.UpdateHardwareTotal(100);
        accumulator.UpdateHardwareTotal(103);

        accumulator.BeginSegment();

        Assert.AreEqual(3, accumulator.Count);
        accumulator.UpdateHardwareTotal(203);
        Assert.AreEqual(3, accumulator.Count);
        accumulator.UpdateHardwareTotal(204);
        Assert.AreEqual(4, accumulator.Count);
    }
}

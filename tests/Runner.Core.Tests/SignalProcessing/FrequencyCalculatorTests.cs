using Runner.Core.SignalProcessing;

namespace Runner.Core.Tests.SignalProcessing;

[TestClass]
public sealed class FrequencyCalculatorTests
{
    [TestMethod]
    [DataRow(90)]
    [DataRow(120)]
    [DataRow(180)]
    [DataRow(210)]
    [DataRow(240)]
    public void GetFrequency_RegularEvents_ReturnsStepsPerMinute(int stepsPerMinute)
    {
        var calculator = new FrequencyCalculator();
        long last = 0;
        for (int step = 0; step <= stepsPerMinute / 2; step++)
        {
            last = (long)Math.Round(step * 60000.0 / stepsPerMinute);
            calculator.AddEvent(last);
        }

        Assert.AreEqual(stepsPerMinute, calculator.GetFrequency(last), 0.05);
    }

    [TestMethod]
    public void GetFrequency_NoEventsOrOneEvent_ReturnsZero()
    {
        var calculator = new FrequencyCalculator();
        Assert.AreEqual(0, calculator.GetFrequency(0));
        calculator.AddEvent(100);
        Assert.AreEqual(0, calculator.GetFrequency(1000));
    }

    [TestMethod]
    public void GetFrequency_BetweenSteps_DoesNotArtificiallyLowerCadence()
    {
        var calculator = At120Spm();

        Assert.AreEqual(120, calculator.GetFrequency(2200), 1e-9);
    }

    [TestMethod]
    public void GetFrequency_NoStepForThreeSeconds_ReturnsExactlyZero()
    {
        var calculator = At120Spm();

        Assert.AreEqual(120, calculator.GetFrequency(4999), 1e-9);
        Assert.AreEqual(0, calculator.GetFrequency(5000));
        Assert.AreEqual(0, calculator.GetFrequency(22000));
    }

    [TestMethod]
    public void GetFrequency_EventsOutsideWindow_DoNotBiasNewCadence()
    {
        var calculator = At120Spm();
        for (long time = 10000; time <= 18000; time += 250)
            calculator.AddEvent(time);

        Assert.AreEqual(240, calculator.GetFrequency(18000), 1e-9);
    }

    [TestMethod]
    public void AddEvent_DuplicateAndOutOfOrderEvents_DoNotChangeCadence()
    {
        var calculator = At120Spm();
        calculator.AddEvent(2000);
        calculator.AddEvent(1999);
        calculator.AddEvent(500);

        Assert.AreEqual(120, calculator.GetFrequency(2000), 1e-9);
    }

    [TestMethod]
    public void Reset_PreviousSession_RemovesAllEventsAndOrderingState()
    {
        var calculator = At120Spm();

        calculator.Reset();

        Assert.AreEqual(0, calculator.GetFrequency(2000));
        calculator.AddEvent(0);
        calculator.AddEvent(1000);
        calculator.AddEvent(2000);
        Assert.AreEqual(60, calculator.GetFrequency(2000), 1e-9);
    }

    [TestMethod]
    public void AddEvents_ZeroBaselineThenHardwareBatches_UsesCountDeltaAndElapsedTime()
    {
        var calculator = new FrequencyCalculator();
        calculator.AddEvents(0, 0);
        calculator.AddEvents(1000, 2);
        calculator.AddEvents(2000, 2);

        Assert.AreEqual(120, calculator.GetFrequency(2000), 1e-9);
    }

    [TestMethod]
    public void AddEvents_FirstBatchWithoutTimeBaseline_DoesNotInventStepTimestamps()
    {
        var calculator = new FrequencyCalculator();
        calculator.AddEvents(1000, 20);
        Assert.AreEqual(0, calculator.GetFrequency(1000));

        calculator.AddEvents(3000, 4);

        Assert.AreEqual(120, calculator.GetFrequency(3000), 1e-9);
    }

    [TestMethod]
    public void AddEvents_ZeroUpdates_DoNotKeepOldCadenceAlive()
    {
        var calculator = new FrequencyCalculator();
        calculator.AddEvents(0, 0);
        calculator.AddEvents(1000, 2);
        calculator.AddEvents(2000, 2);
        calculator.AddEvents(3000, 0);
        calculator.AddEvents(4000, 0);
        calculator.AddEvents(5000, 0);

        Assert.AreEqual(0, calculator.GetFrequency(5000));
    }

    [TestMethod]
    public void AddEvents_NegativeCountAndOutOfOrderBatch_DoNotChangeValidData()
    {
        var calculator = new FrequencyCalculator();
        calculator.AddEvents(0, 0);
        calculator.AddEvents(1000, 2);
        calculator.AddEvents(1000, 50);
        calculator.AddEvents(900, 50);
        calculator.AddEvents(3000, -1);
        calculator.AddEvents(2000, 2);

        Assert.AreEqual(120, calculator.GetFrequency(2000), 1e-9);
    }

    [TestMethod]
    public void GetFrequency_WindowIncludesExactlyEightSecondOldSample()
    {
        var calculator = new FrequencyCalculator();
        calculator.AddEvents(0, 0);
        for (long time = 1000; time <= 7000; time += 1000)
            calculator.AddEvents(time, 2);
        calculator.AddEvents(8000, 4);

        Assert.AreEqual(135, calculator.GetFrequency(8000), 1e-9);
        Assert.AreEqual(960.0 / 7, calculator.GetFrequency(8001), 1e-9);
    }

    [TestMethod]
    public void GetFrequency_MinimumOneSecondObservationWindow_IsInclusive()
    {
        var calculator = new FrequencyCalculator();
        calculator.AddEvents(0, 0);
        calculator.AddEvents(999, 2);
        Assert.AreEqual(0, calculator.GetFrequency(999));

        calculator.AddEvents(1000, 0);

        Assert.AreEqual(120, calculator.GetFrequency(1000), 1e-9);
    }

    [TestMethod]
    public void GetFrequency_BeforeLatestTimestamp_ReturnsZeroWithoutMutatingSamples()
    {
        var calculator = At120Spm();
        calculator.AddEvents(-1, 100);

        Assert.AreEqual(0, calculator.GetFrequency(1999));
        Assert.AreEqual(120, calculator.GetFrequency(2000), 1e-9);
    }

    [TestMethod]
    public void GetFrequency_TimerBeforeFirstHardwareBatch_PreservesZeroCountBaseline()
    {
        var calculator = new FrequencyCalculator();
        calculator.AddEvents(0, 0);

        Assert.AreEqual(0, calculator.GetFrequency(1000));
        calculator.AddEvents(2000, 4);

        Assert.AreEqual(120, calculator.GetFrequency(2000), 1e-9);
    }

    [TestMethod]
    public void GetFrequency_TimerAfterReset_PreservesNewHardwareBaseline()
    {
        var calculator = At120Spm();
        calculator.Reset();
        calculator.AddEvents(3000, 0);

        Assert.AreEqual(0, calculator.GetFrequency(4000));
        Assert.AreEqual(0, calculator.GetFrequency(4500));
        calculator.AddEvents(5000, 4);

        Assert.AreEqual(120, calculator.GetFrequency(5000), 1e-9);
    }

    [TestMethod]
    public void GetFrequency_ExpiredZeroBaselineThenFreshBaseline_RetainsOnlyFreshObservation()
    {
        var calculator = new FrequencyCalculator();
        calculator.AddEvents(0, 0);
        Assert.AreEqual(0, calculator.GetFrequency(9000));
        calculator.AddEvents(10000, 0);

        Assert.AreEqual(0, calculator.GetFrequency(11000));
        calculator.AddEvents(12000, 4);

        Assert.AreEqual(120, calculator.GetFrequency(12000), 1e-9);
    }

    [TestMethod]
    public void GetFrequency_ExtraTimerReadsBetweenBatches_DoNotChangeSubsequentCadence()
    {
        var polled = new FrequencyCalculator();
        var unpolled = new FrequencyCalculator();
        polled.AddEvents(0, 0);
        unpolled.AddEvents(0, 0);

        for (long time = 1000; time <= 24000; time += 1000)
        {
            int count = time <= 12000 ? 2 : 3;
            polled.AddEvents(time, count);
            unpolled.AddEvents(time, count);
            Assert.AreEqual(unpolled.GetFrequency(time), polled.GetFrequency(time), 1e-9,
                $"Extra timer reads must not change the result of the batch at {time} ms.");
            polled.GetFrequency(time + 250);
            polled.GetFrequency(time + 750);
        }

        Assert.AreEqual(180, polled.GetFrequency(24750), 1e-9);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetFrequency_NewZeroBaselineAfterInactivity_SupportsFirstResumedBatch(bool timeoutRead)
    {
        var calculator = new FrequencyCalculator();
        calculator.AddEvents(0, 0);
        calculator.AddEvents(1000, 2);
        Assert.AreEqual(120, calculator.GetFrequency(1000), 1e-9);
        if (timeoutRead)
            Assert.AreEqual(0, calculator.GetFrequency(4000));

        calculator.AddEvents(4000, 0);
        Assert.AreEqual(0, calculator.GetFrequency(5000));
        calculator.AddEvents(6000, 4);

        Assert.AreEqual(120, calculator.GetFrequency(6000), 1e-9);
    }

    private static FrequencyCalculator At120Spm()
    {
        var calculator = new FrequencyCalculator();
        for (long time = 0; time <= 2000; time += 500)
            calculator.AddEvent(time);
        return calculator;
    }
}

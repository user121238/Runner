using Runner.Core.Workouts;

namespace Runner.Core.Tests.Workouts;

[TestClass]
public sealed class WorkoutRunnerTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>法特莱克：2 分钟慢跑 180，1 分钟快走 120 并切歌，共 rounds 轮。</summary>
    private static WorkoutPlan Fartlek(int rounds = 2) => new()
    {
        Name = "法特莱克",
        Rounds = rounds,
        Stages =
        {
            new WorkoutStage { TargetCadence = 180, EndKind = StageEndKind.Duration, EndValue = 120 },
            new WorkoutStage { TargetCadence = 120, EndKind = StageEndKind.Duration, EndValue = 60, SwitchTrackOnEnter = true }
        }
    };

    [TestMethod]
    public void Constructor_RejectsInvalidPlans()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WorkoutRunner(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new WorkoutRunner(new WorkoutPlan { Name = " ", Stages = { new WorkoutStage() } }));
        Assert.ThrowsExactly<ArgumentException>(() => new WorkoutRunner(new WorkoutPlan { Name = "空" }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkoutRunner(new WorkoutPlan { Name = "轮次", Rounds = 0, Stages = { new WorkoutStage() } }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkoutRunner(new WorkoutPlan { Name = "值", Stages = { new WorkoutStage { EndValue = 0 } } }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkoutRunner(new WorkoutPlan { Name = "值", Stages = { new WorkoutStage { EndValue = double.NaN } } }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkoutRunner(new WorkoutPlan { Name = "步频", Stages = { new WorkoutStage { TargetCadence = -1 } } }));
    }

    [TestMethod]
    public void ZeroCadenceStage_IsAllowed()
    {
        var runner = new WorkoutRunner(new WorkoutPlan { Name = "走路", Stages = { new WorkoutStage { TargetCadence = 0 } } });

        runner.Start(S(0), 0);

        Assert.AreEqual(0, runner.Current!.TargetCadence);
    }

    [TestMethod]
    public void NewRunner_IsNotStarted()
    {
        var runner = new WorkoutRunner(Fartlek());

        Assert.IsFalse(runner.IsStarted);
        Assert.IsFalse(runner.IsCompleted);
        Assert.IsNull(runner.Current);
        Assert.AreEqual(4, runner.TotalStages);
        Assert.AreEqual(2, runner.StagesPerRound);
        Assert.AreEqual(0, runner.CurrentRound);
        Assert.AreEqual(0, runner.CurrentStageNumber);
        Assert.AreEqual(WorkoutProgressEvent.None, runner.Update(S(999), 9999));
        Assert.AreEqual(0, runner.Remaining(S(999), 9999));
    }

    [TestMethod]
    public void Start_EntersFirstStage()
    {
        var runner = new WorkoutRunner(Fartlek());

        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Start(S(0), 0));

        Assert.IsTrue(runner.IsStarted);
        Assert.AreEqual(180, runner.Current!.TargetCadence);
        Assert.AreEqual(1, runner.CurrentRound);
        Assert.AreEqual(1, runner.CurrentStageNumber);
        Assert.AreEqual(120, runner.Remaining(S(0), 0));
        Assert.ThrowsExactly<InvalidOperationException>(() => runner.Start(S(0), 0));
    }

    [TestMethod]
    public void Update_AdvancesByDurationAtExactBoundary()
    {
        var runner = new WorkoutRunner(Fartlek());
        runner.Start(S(0), 0);

        Assert.AreEqual(WorkoutProgressEvent.None, runner.Update(S(119.9), 0));
        Assert.AreEqual(180, runner.Current!.TargetCadence);
        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Update(S(120), 0));
        Assert.AreEqual(120, runner.Current!.TargetCadence);
        Assert.IsTrue(runner.Current.SwitchTrackOnEnter);
        Assert.AreEqual(2, runner.CurrentStageNumber);
        Assert.AreEqual(60, runner.Remaining(S(120), 0));
    }

    [TestMethod]
    public void Update_LateSample_DoesNotShiftNextBoundary()
    {
        var runner = new WorkoutRunner(Fartlek());
        runner.Start(S(0), 0);

        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Update(S(125), 0));

        // 第二阶段从计划的 120 秒算起，不是从 125 秒
        Assert.AreEqual(55, runner.Remaining(S(125), 0), 1e-9);
        Assert.AreEqual(WorkoutProgressEvent.None, runner.Update(S(179.9), 0));
        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Update(S(180), 0));
        Assert.AreEqual(2, runner.CurrentRound);
        Assert.AreEqual(1, runner.CurrentStageNumber);
    }

    [TestMethod]
    public void Update_CrossesMultipleStagesInOneCall()
    {
        var runner = new WorkoutRunner(Fartlek());
        runner.Start(S(0), 0);

        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Update(S(200), 0));

        Assert.AreEqual(2, runner.CurrentRound);
        Assert.AreEqual(1, runner.CurrentStageNumber);
        Assert.AreEqual(180, runner.Current!.TargetCadence);
        Assert.AreEqual(100, runner.Remaining(S(200), 0), 1e-9);
    }

    [TestMethod]
    public void Update_CompletesAfterLastStage()
    {
        var runner = new WorkoutRunner(Fartlek());
        runner.Start(S(0), 0);

        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Update(S(359.9), 0));
        Assert.AreEqual(2, runner.CurrentRound);
        Assert.AreEqual(2, runner.CurrentStageNumber);
        Assert.AreEqual(0.1, runner.Remaining(S(359.9), 0), 1e-9);
        Assert.AreEqual(WorkoutProgressEvent.Completed, runner.Update(S(360), 0));

        Assert.IsTrue(runner.IsCompleted);
        Assert.IsNull(runner.Current);
        Assert.AreEqual(2, runner.CurrentRound);
        Assert.AreEqual(2, runner.CurrentStageNumber);
        Assert.AreEqual(0, runner.Remaining(S(360), 0));
        Assert.AreEqual(WorkoutProgressEvent.None, runner.Update(S(400), 0));
    }

    [TestMethod]
    public void Update_DistanceStages_AdvanceByMeters()
    {
        var plan = new WorkoutPlan
        {
            Name = "距离",
            Stages =
            {
                new WorkoutStage { TargetCadence = 180, EndKind = StageEndKind.Distance, EndValue = 1000 },
                new WorkoutStage { TargetCadence = 150, EndKind = StageEndKind.Distance, EndValue = 500 }
            }
        };
        var runner = new WorkoutRunner(plan);
        runner.Start(S(0), 0);

        Assert.AreEqual(WorkoutProgressEvent.None, runner.Update(S(300), 999.9));
        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Update(S(300), 1000));
        Assert.AreEqual(150, runner.Current!.TargetCadence);
        Assert.AreEqual(500, runner.Remaining(S(300), 1000));
        Assert.AreEqual(WorkoutProgressEvent.Completed, runner.Update(S(600), 1700));
    }

    [TestMethod]
    public void Update_MixedKinds_StartNextStageFromActualOtherDimension()
    {
        var plan = new WorkoutPlan
        {
            Name = "混合",
            Stages =
            {
                new WorkoutStage { EndKind = StageEndKind.Duration, EndValue = 60 },
                new WorkoutStage { EndKind = StageEndKind.Distance, EndValue = 200 }
            }
        };
        var runner = new WorkoutRunner(plan);
        runner.Start(S(0), 0);

        // 时长阶段晚到 10 秒结束，距离阶段从当时的实际距离 150 米起算
        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Update(S(70), 150));
        Assert.AreEqual(200, runner.Remaining(S(70), 150));
        Assert.AreEqual(WorkoutProgressEvent.None, runner.Update(S(100), 349.9));
        Assert.AreEqual(WorkoutProgressEvent.Completed, runner.Update(S(100), 350));
    }

    [TestMethod]
    public void Rounds_RepeatTheStageList()
    {
        var plan = new WorkoutPlan { Name = "三轮", Rounds = 3, Stages = { new WorkoutStage { EndValue = 10 } } };
        var runner = new WorkoutRunner(plan);
        runner.Start(S(0), 0);

        Assert.AreEqual(3, runner.TotalStages);
        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Update(S(10), 0));
        Assert.AreEqual(2, runner.CurrentRound);
        Assert.AreEqual(WorkoutProgressEvent.StageStarted, runner.Update(S(20), 0));
        Assert.AreEqual(3, runner.CurrentRound);
        Assert.AreEqual(1, runner.CurrentStageNumber);
        Assert.AreEqual(WorkoutProgressEvent.Completed, runner.Update(S(30), 0));
    }
}

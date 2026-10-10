using LiteDB;
using Runner.Business.Services.Activities;
using Runner.Business.Services.Workouts;
using Runner.Core.Activities;
using Runner.Core.Workouts;

namespace Runner.Core.Tests.Storage;

/// <summary>真实文件与 Shared 连接配置的回归；桌面/Mac 通过不等于 iPhone AOT 验证。</summary>
[TestClass]
[DoNotParallelize]
public sealed class SharedStoreTests
{
    private static readonly DateTime StartUtc = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
    private readonly string _temporaryRoot;
    private readonly string _fixtureName;
    private readonly string _fixtureDirectory;
    private readonly string _databasePath;

    public SharedStoreTests()
    {
        _temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        _fixtureName = $"Runner.SharedStoreTests-{Guid.NewGuid():N}";
        _fixtureDirectory = Path.Combine(_temporaryRoot, _fixtureName);
        _databasePath = Path.Combine(_fixtureDirectory, "shared-test.db");
        Directory.CreateDirectory(_fixtureDirectory);
    }

    [TestMethod]
    public async Task GetAll_EmptySharedDatabase_ReturnsEmptyLists()
    {
        using var database = Open();
        var workouts = new WorkoutStore(database);
        var activities = new ActivityStore(database);

        Assert.IsEmpty(await workouts.GetAllAsync());
        Assert.IsEmpty(await activities.GetAllAsync());
        Assert.IsEmpty(database.GetCollectionNames());
        Assert.IsTrue(File.Exists(_databasePath));
    }

    [TestMethod]
    public async Task Save_DisposeAndReopenSharedDatabase_PreservesModelsAndIntegerIds()
    {
        using (var database = Open())
        {
            var plan = Plan("Shared course");
            var record = Record();

            await new WorkoutStore(database).SaveAsync(plan);
            await new ActivityStore(database).SaveAsync(record);

            Assert.AreEqual(1, plan.Id);
            Assert.AreEqual(1, record.Id);
            var planDocument = database.GetCollection("WorkoutPlan").FindById(1);
            var recordDocument = database.GetCollection("ActivityRecord").FindById(1);
            Assert.IsNotNull(planDocument);
            Assert.IsNotNull(recordDocument);
            Assert.AreEqual(BsonType.Int32, planDocument["_id"].Type);
            Assert.AreEqual(BsonType.Int32, recordDocument["_id"].Type);
        }

        using var reopened = Open();
        var loadedPlan = await new WorkoutStore(reopened).GetAsync(1);
        var loadedRecord = await new ActivityStore(reopened).GetAsync(1);

        Assert.IsNotNull(loadedPlan);
        Assert.AreEqual("Shared course", loadedPlan.Name);
        Assert.AreEqual(2, loadedPlan.Rounds);
        var stage = Assert.ContainsSingle(loadedPlan.Stages);
        Assert.AreEqual(StageEndKind.Distance, stage.EndKind);
        Assert.AreEqual(400, stage.EndValue);
        Assert.IsTrue(stage.SwitchTrackOnEnter);
        Assert.IsNotNull(loadedRecord);
        Assert.AreEqual("Shared course", loadedRecord.WorkoutName);
        Assert.AreEqual(StartUtc, loadedRecord.StartTimeUtc.ToUniversalTime());
        Assert.AreEqual(400, loadedRecord.DistanceMeters);
        Assert.AreEqual(360, loadedRecord.StepCount);
        Assert.AreEqual(31.2, Assert.ContainsSingle(loadedRecord.TrackPoints).Latitude);
        Assert.AreEqual(180, Assert.ContainsSingle(loadedRecord.CadencePoints).StepsPerMinute);
        Assert.AreEqual(400, Assert.ContainsSingle(loadedRecord.DistanceSamples).Meters);
        Assert.HasCount(2, reopened.GetCollectionNames());
    }

    [TestMethod]
    public async Task UpdateAndDelete_SharedInstances_SeeChangesAndKeepRemainingIds()
    {
        using var first = Open();
        using var second = Open();
        var firstWorkouts = new WorkoutStore(first);
        var secondWorkouts = new WorkoutStore(second);
        var firstActivities = new ActivityStore(first);
        var secondActivities = new ActivityStore(second);
        var removedPlan = await firstWorkouts.SaveAsync(Plan("Original"));
        var keptPlan = await firstWorkouts.SaveAsync(Plan("Kept"));
        var removedRecord = await firstActivities.SaveAsync(Record());
        var keptRecord = await firstActivities.SaveAsync(Record());

        var plan = await secondWorkouts.GetAsync(removedPlan.Id);
        var record = await secondActivities.GetAsync(removedRecord.Id);
        Assert.IsNotNull(plan);
        Assert.IsNotNull(record);
        plan.Name = "Updated";
        plan.Stages[0].EndValue = 800;
        record.DistanceMeters = 800;

        await secondWorkouts.SaveAsync(plan);
        await secondActivities.SaveAsync(record);

        var updatedPlan = await firstWorkouts.GetAsync(removedPlan.Id);
        var updatedRecord = await firstActivities.GetAsync(removedRecord.Id);
        Assert.IsNotNull(updatedPlan);
        Assert.IsNotNull(updatedRecord);
        Assert.AreEqual("Updated", updatedPlan.Name);
        Assert.AreEqual(800, updatedPlan.Stages[0].EndValue);
        Assert.AreEqual(800, updatedRecord.DistanceMeters);
        Assert.HasCount(2, await firstWorkouts.GetAllAsync());
        Assert.HasCount(2, await firstActivities.GetAllAsync());

        await secondWorkouts.DeleteAsync(removedPlan.Id);
        await secondActivities.DeleteAsync(removedRecord.Id);

        Assert.IsNull(await firstWorkouts.GetAsync(removedPlan.Id));
        Assert.IsNull(await firstActivities.GetAsync(removedRecord.Id));
        Assert.AreEqual(keptPlan.Id, Assert.ContainsSingle(await firstWorkouts.GetAllAsync()).Id);
        Assert.AreEqual(keptRecord.Id, Assert.ContainsSingle(await firstActivities.GetAllAsync()).Id);
        Assert.AreEqual(3, (await firstWorkouts.SaveAsync(Plan("Next"))).Id);
        Assert.AreEqual(3, (await firstActivities.SaveAsync(Record())).Id);
    }

    private LiteDatabase Open() => new(new ConnectionString
    {
        Filename = _databasePath,
        Connection = ConnectionType.Shared
    }, new BsonMapper());

    private static WorkoutPlan Plan(string name) => new()
    {
        Name = name,
        Rounds = 2,
        Stages = [new WorkoutStage { TargetCadence = 180, EndKind = StageEndKind.Distance, EndValue = 400, SwitchTrackOnEnter = true }]
    };

    private static ActivityRecord Record() => new()
    {
        StartTimeUtc = StartUtc,
        EndTimeUtc = StartUtc.AddSeconds(120),
        ElapsedSeconds = 120,
        DistanceMeters = 400,
        StepCount = 360,
        AverageCadence = 180,
        AverageSpeedKmh = 12,
        WorkoutName = "Shared course",
        TrackPoints = [new TrackPoint { Latitude = 31.2, Longitude = 121.5, TimestampUtc = StartUtc }],
        CadencePoints = [new CadencePoint { TimeUtc = StartUtc, StepsPerMinute = 180 }],
        DistanceSamples = [new DistancePoint { ElapsedSeconds = 120, Meters = 400 }]
    };

    [TestCleanup]
    public void DeleteFixtureDirectory()
    {
        var directory = new DirectoryInfo(Path.GetFullPath(_fixtureDirectory));
        // 递归删除前验证它仍是我们创建的临时根目录的唯一子目录，且没有变成目录链接。
        if (!string.Equals(directory.Parent?.FullName, _temporaryRoot, StringComparison.Ordinal)
            || directory.Name != _fixtureName || directory.LinkTarget != null)
            throw new InvalidOperationException("临时数据库清理路径与创建时不一致");
        if (directory.Exists) directory.Delete(recursive: true);
    }
}

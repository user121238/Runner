using LiteDB;
using Runner.Business.Services.Activities;
using Runner.Business.Services.Workouts;
using Runner.Core.Activities;
using Runner.Core.Workouts;

namespace Runner.Core.Tests.Storage;

[TestClass]
public sealed class StoreCompatibilityTests
{
    private static readonly DateTime StartUtc = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task LegacyDatabase_DefaultCollectionNamesAndMissingPaceSamplesRemainReadable()
    {
        using var database = Reopen(CreateLegacyDatabase());
        var activities = new ActivityStore(database);
        var workouts = new WorkoutStore(database);

        var record = await activities.GetAsync(42);
        var plan = await workouts.GetAsync(7);

        Assert.IsNotNull(record);
        Assert.AreEqual(42, record.Id);
        Assert.AreEqual(StartUtc, record.StartTimeUtc.ToUniversalTime());
        Assert.AreEqual(5000, record.DistanceMeters);
        Assert.AreEqual(1800, record.ElapsedSeconds);
        Assert.AreEqual(5400, record.StepCount);
        Assert.AreEqual("Tempo", record.WorkoutName);
        Assert.IsEmpty(record.DistanceSamples);
        var point = Assert.ContainsSingle(record.TrackPoints);
        Assert.AreEqual(31.2, point.Latitude);
        Assert.AreEqual(121.5, point.Longitude);
        Assert.AreEqual(5, point.HorizontalAccuracyMeters);
        var cadence = Assert.ContainsSingle(record.CadencePoints);
        Assert.AreEqual(180, cadence.StepsPerMinute);
        Assert.AreEqual(StartUtc, cadence.TimeUtc.ToUniversalTime());

        Assert.IsNotNull(plan);
        Assert.AreEqual(7, plan.Id);
        Assert.AreEqual("Legacy course", plan.Name);
        Assert.AreEqual(3, plan.Rounds);
        var stage = Assert.ContainsSingle(plan.Stages);
        Assert.AreEqual(175, stage.TargetCadence);
        Assert.AreEqual(StageEndKind.Distance, stage.EndKind);
        Assert.AreEqual(500, stage.EndValue);
        Assert.IsTrue(stage.SwitchTrackOnEnter);
    }

    [TestMethod]
    public async Task ActivitySave_NewRecordBackfillsIntegerIdAndPersistsAllSamples()
    {
        using var stream = new MemoryStream();
        int id;
        using (var database = Open(stream))
        {
            var store = new ActivityStore(database);
            var record = Record();
            int changes = 0;
            store.Changed += () => changes++;

            var saved = await store.SaveAsync(record);

            Assert.AreSame(record, saved);
            Assert.AreEqual(1, record.Id);
            Assert.AreEqual(1, changes);
            Assert.AreEqual("ActivityRecord", Assert.ContainsSingle(database.GetCollectionNames()));
            id = record.Id;
        }

        using var reopened = Reopen(stream.ToArray());
        var loaded = await new ActivityStore(reopened).GetAsync(id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(id, loaded.Id);
        Assert.AreEqual(5000, loaded.DistanceMeters);
        Assert.AreEqual(180, loaded.AverageCadence);
        Assert.AreEqual(10, loaded.AverageSpeedKmh);
        Assert.AreEqual(StartUtc.AddMinutes(30), loaded.EndTimeUtc.ToUniversalTime());
        Assert.HasCount(1, loaded.TrackPoints);
        Assert.HasCount(1, loaded.CadencePoints);
        Assert.HasCount(2, loaded.DistanceSamples);
        Assert.AreEqual(0, loaded.DistanceSamples[0].Meters);
        Assert.AreEqual(1800, loaded.DistanceSamples[1].ElapsedSeconds);
        Assert.AreEqual(5000, loaded.DistanceSamples[1].Meters);
    }

    [TestMethod]
    public async Task ActivitySave_UpdatesLegacyIdAndKeepsDescendingOrder()
    {
        using var database = Reopen(CreateLegacyDatabase());
        var store = new ActivityStore(database);
        var older = await store.GetAsync(42);
        Assert.IsNotNull(older);
        int changes = 0;
        store.Changed += () => changes++;
        older.WorkoutName = "Updated";
        older.DistanceSamples.Add(new DistancePoint { ElapsedSeconds = 1800, Meters = 5000 });
        var newer = Record();
        newer.StartTimeUtc = StartUtc.AddDays(1);

        await store.SaveAsync(older);
        await store.SaveAsync(newer);
        var records = await store.GetAllAsync();

        Assert.AreEqual(42, older.Id);
        Assert.AreEqual(43, newer.Id);
        Assert.HasCount(2, records);
        Assert.AreEqual(43, records[0].Id);
        Assert.AreEqual(42, records[1].Id);
        Assert.AreEqual("Updated", records[1].WorkoutName);
        Assert.HasCount(1, records[1].DistanceSamples);
        Assert.AreEqual(2, changes);

        await store.DeleteAsync(42);

        Assert.IsNull(await store.GetAsync(42));
        Assert.AreEqual(43, Assert.ContainsSingle(await store.GetAllAsync()).Id);
        Assert.AreEqual(3, changes);
        Assert.HasCount(2, database.GetCollectionNames());
    }

    [TestMethod]
    public async Task WorkoutSave_NewAndLegacyIdsPersistWithoutRenamingCollection()
    {
        using var stream = StreamFrom(CreateLegacyDatabase());
        int newId;
        using (var database = Open(stream))
        {
            var store = new WorkoutStore(database);
            var existing = await store.GetAsync(7);
            Assert.IsNotNull(existing);
            existing.Name = "Zulu";
            existing.Stages[0].EndValue = 750;
            var added = Plan("Alpha");
            int changes = 0;
            store.Changed += () => changes++;

            await store.SaveAsync(existing);
            var saved = await store.SaveAsync(added);
            var plans = await store.GetAllAsync();

            Assert.AreSame(added, saved);
            Assert.AreEqual(7, existing.Id);
            Assert.AreEqual(8, added.Id);
            Assert.HasCount(2, plans);
            Assert.AreEqual("Alpha", plans[0].Name);
            Assert.AreEqual("Zulu", plans[1].Name);
            Assert.AreEqual(750, plans[1].Stages[0].EndValue);
            Assert.AreEqual(2, changes);
            Assert.Contains("WorkoutPlan", database.GetCollectionNames());
            Assert.HasCount(2, database.GetCollectionNames());
            newId = added.Id;
        }

        using var reopened = Reopen(stream.ToArray());
        var storeAfterRestart = new WorkoutStore(reopened);
        var loaded = await storeAfterRestart.GetAsync(newId);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("Alpha", loaded.Name);
        Assert.AreEqual(3, loaded.Rounds);
        Assert.IsTrue(loaded.Stages[0].SwitchTrackOnEnter);
        Assert.AreEqual(750, (await storeAfterRestart.GetAsync(7))!.Stages[0].EndValue);
    }

    [TestMethod]
    public async Task WorkoutDelete_RemovesOnlyRequestedLegacyRecordAndNotifies()
    {
        using var database = Reopen(CreateLegacyDatabase());
        var store = new WorkoutStore(database);
        var added = Plan("Kept");
        await store.SaveAsync(added);
        int changes = 0;
        store.Changed += () => changes++;

        await store.DeleteAsync(7);

        Assert.IsNull(await store.GetAsync(7));
        Assert.AreEqual(added.Id, Assert.ContainsSingle(await store.GetAllAsync()).Id);
        Assert.IsNotNull(await new ActivityStore(database).GetAsync(42));
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public async Task WorkoutSave_InvalidPlanDoesNotWriteOrNotify()
    {
        using var database = Open(new MemoryStream());
        var store = new WorkoutStore(database);
        var invalid = Plan("");
        int changes = 0;
        store.Changed += () => changes++;

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await store.SaveAsync(invalid));

        Assert.AreEqual(0, invalid.Id);
        Assert.IsEmpty(await store.GetAllAsync());
        Assert.AreEqual(0, changes);
    }

    private static byte[] CreateLegacyDatabase()
    {
        using var stream = new MemoryStream();
        using (var database = Open(stream))
        {
            // DM 1.0.4 的仓储使用无参数 GetCollection<T>()，没有自定义映射。
            var activities = database.GetCollection<ActivityRecord>();
            var workouts = database.GetCollection<WorkoutPlan>();
            activities.Insert(Record(42));
            workouts.Insert(Plan("Legacy course", 7));

            // 配速上线前的文档没有这个字段，不能只用当前类型的空列表代替旧格式。
            var documents = database.GetCollection(activities.Name);
            var document = documents.FindById(42);
            document.Remove(nameof(ActivityRecord.DistanceSamples));
            documents.Update(document);
        }
        return stream.ToArray();
    }

    // 默认映射配置与旧仓储相同；独立 mapper 避免并行测试竞用 Global 的首次映射缓存。
    private static LiteDatabase Open(MemoryStream stream) => new(stream, new BsonMapper());

    private static LiteDatabase Reopen(byte[] bytes) => Open(StreamFrom(bytes));

    private static MemoryStream StreamFrom(byte[] bytes)
    {
        var stream = new MemoryStream();
        stream.Write(bytes);
        stream.Position = 0;
        return stream;
    }

    private static ActivityRecord Record(int id = 0) => new()
    {
        Id = id,
        StartTimeUtc = StartUtc,
        EndTimeUtc = StartUtc.AddMinutes(30),
        ElapsedSeconds = 1800,
        DistanceMeters = 5000,
        StepCount = 5400,
        AverageCadence = 180,
        AverageSpeedKmh = 10,
        WorkoutName = "Tempo",
        TrackPoints =
        {
            new TrackPoint { Latitude = 31.2, Longitude = 121.5, TimestampUtc = StartUtc, HorizontalAccuracyMeters = 5 }
        },
        CadencePoints =
        {
            new CadencePoint { TimeUtc = StartUtc, StepsPerMinute = 180 }
        },
        DistanceSamples =
        {
            new DistancePoint { ElapsedSeconds = 0, Meters = 0 },
            new DistancePoint { ElapsedSeconds = 1800, Meters = 5000 }
        }
    };

    private static WorkoutPlan Plan(string name, int id = 0) => new()
    {
        Id = id,
        Name = name,
        Rounds = 3,
        Stages =
        {
            new WorkoutStage { TargetCadence = 175, EndKind = StageEndKind.Distance, EndValue = 500, SwitchTrackOnEnter = true }
        }
    };
}

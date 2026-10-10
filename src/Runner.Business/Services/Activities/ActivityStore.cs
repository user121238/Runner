using LiteDB;
using Runner.Core.Activities;

namespace Runner.Business.Services.Activities;

/// <summary>跑步记录的存取；保存或删除后触发 Changed，供记录页刷新。</summary>
public sealed class ActivityStore
{
    private readonly ILiteCollection<ActivityRecord> _records;

    /// <summary>调用线程不定。</summary>
    public event Action? Changed;

    public ActivityStore(ILiteDatabase database)
    {
        // 旧 DM 仓储使用 GetCollection<ActivityRecord>()；固定名称，沿用已有 runner.db。
        _records = database.GetCollection<ActivityRecord>("ActivityRecord");
    }

    /// <summary>按开始时间倒序。</summary>
    public Task<IReadOnlyList<ActivityRecord>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<ActivityRecord>>(_records.FindAll().OrderByDescending(r => r.StartTimeUtc).ToList());

    public Task<ActivityRecord?> GetAsync(int id) => Task.FromResult<ActivityRecord?>(_records.FindById(id));

    public Task<ActivityRecord> SaveAsync(ActivityRecord record)
    {
        if (record.Id == 0) _records.Insert(record);
        else _records.Update(record);
        Changed?.Invoke();
        return Task.FromResult(record);
    }

    public Task DeleteAsync(int id)
    {
        _records.Delete(id);
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}

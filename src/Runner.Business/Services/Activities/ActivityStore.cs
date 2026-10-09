using DM.Maui.Framework.Abstractions.Data;
using Runner.Core.Activities;

namespace Runner.Business.Services.Activities;

/// <summary>跑步记录的存取；保存或删除后触发 Changed，供记录页刷新。</summary>
public sealed class ActivityStore
{
    private readonly IRepository<ActivityRecord> _repository;

    /// <summary>调用线程不定。</summary>
    public event Action? Changed;

    public ActivityStore(IRepository<ActivityRecord> repository)
    {
        _repository = repository;
    }

    /// <summary>按开始时间倒序。</summary>
    public async Task<IReadOnlyList<ActivityRecord>> GetAllAsync()
    {
        var records = await _repository.GetAllAsync();
        return records.OrderByDescending(r => r.StartTimeUtc).ToList();
    }

    public Task<ActivityRecord?> GetAsync(int id) => _repository.GetByIdAsync(id);

    public async Task<ActivityRecord> SaveAsync(ActivityRecord record)
    {
        var saved = record.Id == 0
            ? await _repository.InsertAsync(record)
            : await _repository.UpdateAsync(record);
        Changed?.Invoke();
        return saved;
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        Changed?.Invoke();
    }
}

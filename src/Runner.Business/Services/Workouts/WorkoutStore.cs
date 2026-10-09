using DM.Maui.Framework.Abstractions.Data;
using Runner.Core.Workouts;

namespace Runner.Business.Services.Workouts;

/// <summary>课程的增删改查；保存或删除后触发 Changed，供列表与首页刷新。</summary>
public sealed class WorkoutStore
{
    private readonly IRepository<WorkoutPlan> _repository;

    /// <summary>调用线程不定。</summary>
    public event Action? Changed;

    public WorkoutStore(IRepository<WorkoutPlan> repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<WorkoutPlan>> GetAllAsync()
    {
        var plans = await _repository.GetAllAsync();
        return plans.OrderBy(p => p.Name).ToList();
    }

    public Task<WorkoutPlan?> GetAsync(int id) => _repository.GetByIdAsync(id);

    /// <summary>校验后保存；Id 为 0 时新增并回填 Id。</summary>
    public async Task<WorkoutPlan> SaveAsync(WorkoutPlan plan)
    {
        plan.Validate();
        var saved = plan.Id == 0
            ? await _repository.InsertAsync(plan)
            : await _repository.UpdateAsync(plan);
        Changed?.Invoke();
        return saved;
    }

    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
        Changed?.Invoke();
    }
}

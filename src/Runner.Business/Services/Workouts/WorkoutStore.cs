using LiteDB;
using Runner.Core.Workouts;

namespace Runner.Business.Services.Workouts;

/// <summary>课程的增删改查；保存或删除后触发 Changed，供列表与首页刷新。</summary>
public sealed class WorkoutStore
{
    private readonly ILiteCollection<WorkoutPlan> _plans;

    /// <summary>调用线程不定。</summary>
    public event Action? Changed;

    public WorkoutStore(ILiteDatabase database)
    {
        // 旧 DM 仓储使用 GetCollection<WorkoutPlan>()；固定名称，沿用已有 runner.db。
        _plans = database.GetCollection<WorkoutPlan>("WorkoutPlan");
    }

    public Task<IReadOnlyList<WorkoutPlan>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<WorkoutPlan>>(_plans.FindAll().OrderBy(p => p.Name).ToList());

    public Task<WorkoutPlan?> GetAsync(int id) => Task.FromResult<WorkoutPlan?>(_plans.FindById(id));

    /// <summary>校验后保存；Id 为 0 时新增并回填 Id。</summary>
    public Task<WorkoutPlan> SaveAsync(WorkoutPlan plan)
    {
        plan.Validate();
        if (plan.Id == 0) _plans.Insert(plan);
        else _plans.Update(plan);
        Changed?.Invoke();
        return Task.FromResult(plan);
    }

    public Task DeleteAsync(int id)
    {
        _plans.Delete(id);
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}

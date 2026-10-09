namespace Runner.Core.Workouts;

/// <summary>
/// 按会话的跑动时长与累计距离推进课程阶段。阶段边界按计划值累加（时长阶段按时长、距离阶段按距离），
/// 采样晚到不会把误差带进下一阶段；一次更新可跨多个阶段。
/// </summary>
public sealed class WorkoutRunner
{
    private readonly WorkoutStage[] _stages;
    private readonly int _rounds;
    private int _position = -1;
    private TimeSpan _stageStartElapsed;
    private double _stageStartDistance;

    public WorkoutRunner(WorkoutPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();
        Name = plan.Name;
        _rounds = plan.Rounds;
        _stages = plan.Stages.ToArray();
    }

    public string Name { get; }
    public int TotalStages => _stages.Length * _rounds;
    public int StagesPerRound => _stages.Length;
    public bool IsStarted => _position >= 0;
    public bool IsCompleted { get; private set; }

    /// <summary>当前阶段；未开始或已完成时为 null。</summary>
    public WorkoutStage? Current => IsStarted && !IsCompleted ? _stages[_position % _stages.Length] : null;

    /// <summary>从 1 开始的轮次，供界面显示；完成后停在最后一轮。</summary>
    public int CurrentRound => IsStarted ? ClampedPosition / _stages.Length + 1 : 0;

    /// <summary>从 1 开始的本轮阶段序号，供界面显示；完成后停在最后一个。</summary>
    public int CurrentStageNumber => IsStarted ? ClampedPosition % _stages.Length + 1 : 0;

    private int ClampedPosition => Math.Min(_position, TotalStages - 1);

    /// <summary>进入第一阶段。</summary>
    public WorkoutProgressEvent Start(TimeSpan elapsed, double distanceMeters)
    {
        if (IsStarted) throw new InvalidOperationException("课程已开始");
        _position = 0;
        _stageStartElapsed = elapsed;
        _stageStartDistance = distanceMeters;
        return WorkoutProgressEvent.StageStarted;
    }

    /// <summary>用会话的跑动时长与累计距离推进；返回本次更新最后发生的事件。</summary>
    public WorkoutProgressEvent Update(TimeSpan elapsed, double distanceMeters)
    {
        if (!IsStarted || IsCompleted) return WorkoutProgressEvent.None;

        var result = WorkoutProgressEvent.None;
        while (true)
        {
            var stage = _stages[_position % _stages.Length];
            if (Consumed(stage, elapsed, distanceMeters) < stage.EndValue) return result;

            // 边界按计划值累加，另一维取实际值
            if (stage.EndKind == StageEndKind.Duration)
            {
                _stageStartElapsed += TimeSpan.FromSeconds(stage.EndValue);
                _stageStartDistance = distanceMeters;
            }
            else
            {
                _stageStartDistance += stage.EndValue;
                _stageStartElapsed = elapsed;
            }

            _position++;
            if (_position >= TotalStages)
            {
                IsCompleted = true;
                return WorkoutProgressEvent.Completed;
            }
            result = WorkoutProgressEvent.StageStarted;
        }
    }

    /// <summary>当前阶段剩余量：时长阶段为秒，距离阶段为米；未进行时为 0。</summary>
    public double Remaining(TimeSpan elapsed, double distanceMeters)
    {
        var stage = Current;
        if (stage == null) return 0;
        return Math.Max(0, stage.EndValue - Consumed(stage, elapsed, distanceMeters));
    }

    private double Consumed(WorkoutStage stage, TimeSpan elapsed, double distanceMeters) =>
        stage.EndKind == StageEndKind.Duration
            ? (elapsed - _stageStartElapsed).TotalSeconds
            : distanceMeters - _stageStartDistance;
}

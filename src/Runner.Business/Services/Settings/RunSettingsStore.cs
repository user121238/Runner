using LiteDB;
using Runner.Business.Models;

namespace Runner.Business.Services.Settings;

/// <summary>
/// 运动设置的唯一入口：内存持有当前值，改动即写入 LiteDB。
/// 直接使用框架注册的单例 ILiteDatabase：框架的 IUnitOfWork 是作用域生命周期，应用级单例无法持有。
/// </summary>
public sealed class RunSettingsStore
{
    private const string CollectionName = "run_settings";
    private readonly ILiteDatabase _database;
    private readonly object _sync = new();
    private RunSettings? _current;

    /// <summary>保存成功后触发，参数为新值副本；调用线程不定。</summary>
    public event Action<RunSettings>? Changed;

    public RunSettingsStore(ILiteDatabase database)
    {
        _database = database;
    }

    /// <summary>当前设置的副本；首次访问时从数据库加载，没有则用默认值。</summary>
    public RunSettings Current
    {
        get
        {
            lock (_sync)
            {
                _current ??= Load();
                return _current.Clone();
            }
        }
    }

    public Task SaveAsync(RunSettings settings)
    {
        var normalized = Normalize(settings);
        return Task.Run(() =>
        {
            lock (_sync)
            {
                Collection.Upsert(normalized);
                _current = normalized;
            }
            Changed?.Invoke(normalized.Clone());
        });
    }

    /// <summary>把越界值夹到可用范围，Id 固定。</summary>
    public static RunSettings Normalize(RunSettings settings) => new()
    {
        Id = RunSettings.SingletonId,
        TargetCadence = Math.Clamp(settings.TargetCadence, 100, 240),
        ToleranceSpm = Math.Clamp(settings.ToleranceSpm, 2, 40),
        AudioAlertEnabled = settings.AudioAlertEnabled,
        VibrationAlertEnabled = settings.VibrationAlertEnabled
    };

    private ILiteCollection<RunSettings> Collection => _database.GetCollection<RunSettings>(CollectionName);

    private RunSettings Load() => Collection.FindById(RunSettings.SingletonId) ?? new RunSettings();
}

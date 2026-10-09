using Runner.Business.Models;
using Runner.Business.Platforms.Android.Media;
using Runner.Core.Alerts;

namespace Runner.Business.Platforms.Android.Alerts;

/// <summary>把步频判定结果变成声音与震动；开关来自设置。</summary>
public sealed class CadenceAlertNotifier
{
    private readonly CadencePlay _metronome;
    private readonly AndroidVibrator _vibrator;
    private bool _audioEnabled = true;
    private bool _vibrationEnabled = true;

    public CadenceAlertNotifier(CadencePlay metronome, AndroidVibrator vibrator)
    {
        _metronome = metronome;
        _vibrator = vibrator;
    }

    public void Apply(RunSettings settings)
    {
        _audioEnabled = settings.AudioAlertEnabled;
        _vibrationEnabled = settings.VibrationAlertEnabled;
        if (!_audioEnabled) _metronome.TickRate = 1f;
    }

    /// <summary>偏离状态变化时调整节拍音调：过慢升调催促，过快降调。</summary>
    public void SetState(CadenceAlertKind state)
    {
        _metronome.TickRate = _audioEnabled
            ? state switch
            {
                CadenceAlertKind.TooSlow => 1.2f,
                CadenceAlertKind.TooFast => 0.85f,
                _ => 1f
            }
            : 1f;
    }

    public void Notify(CadenceAlertKind alert)
    {
        if (alert == CadenceAlertKind.None) return;
        if (_audioEnabled) _metronome.PlayCue(alert);
        if (_vibrationEnabled) _vibrator.Vibrate(alert);
    }
}

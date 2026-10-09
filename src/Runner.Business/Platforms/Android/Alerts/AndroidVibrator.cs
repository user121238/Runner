using Android.Content;
using Android.OS;
using Runner.Core.Alerts;
using Application = Android.App.Application;

namespace Runner.Business.Platforms.Android.Alerts;

/// <summary>震动提醒：过慢一次长振，过快两次短振。</summary>
public sealed class AndroidVibrator
{
    private static readonly long[] TooSlowPattern = { 0, 400 };
    private static readonly long[] TooFastPattern = { 0, 120, 100, 120 };
    private readonly Vibrator? _vibrator = Resolve();

    public void Vibrate(CadenceAlertKind kind)
    {
        var pattern = kind switch
        {
            CadenceAlertKind.TooSlow => TooSlowPattern,
            CadenceAlertKind.TooFast => TooFastPattern,
            _ => null
        };
        if (pattern == null || _vibrator == null || !_vibrator.HasVibrator) return;

        var effect = VibrationEffect.CreateWaveform(pattern, -1);
        if (effect != null) _vibrator.Vibrate(effect);
    }

    private static Vibrator? Resolve()
    {
        var context = Application.Context;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var manager = context.GetSystemService(Context.VibratorManagerService) as VibratorManager;
            return manager?.DefaultVibrator;
        }
#pragma warning disable CA1422 // VibratorService 自 31 起过时，低版本仍需使用
        return context.GetSystemService(Context.VibratorService) as Vibrator;
#pragma warning restore CA1422
    }
}

namespace Runner.Business.Platforms.Android.Media;

/// <summary>节拍与提醒音的 Android 资源 Id；提醒音传 0 表示不加载。</summary>
public readonly record struct MetronomeSounds(int TickResourceId, int TooSlowCueResourceId, int TooFastCueResourceId);

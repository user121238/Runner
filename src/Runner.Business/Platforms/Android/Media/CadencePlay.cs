using Android.Content;
using Android.Media;
using Android.OS;
using Java.Lang;
using Runner.Core.Alerts;
using Application = Android.App.Application;
using Debug = System.Diagnostics.Debug;
using Math = System.Math;

namespace Runner.Business.Platforms.Android.Media
{
    /// <summary>
    /// 步频节拍与提醒音播放器：用 SoundPool 与音乐混音播放；节拍可变调，提醒音单次播放。
    /// </summary>
    public class CadencePlay : IDisposable
    {
        /// <summary>节拍与提醒音可能同时发声</summary>
        private const int MAX_STREAMS = 2;

        private readonly Lock _lockObject = new();
        private readonly HashSet<int> _loaded = new();
        private SoundPool? _soundPool;
        private int _tickSound = -1;
        private int _tooSlowCue = -1;
        private int _tooFastCue = -1;
        private float _tickRate = 1f;

        private Handler? _handler;
        private Runnable? _tickRunnable;
        private long _nextTickTime;
        private bool _running;
        private int _intervalMs;

        /// <summary>节拍播放速率即音调：1 为原音，范围 0.5–2。</summary>
        public float TickRate
        {
            get => _tickRate;
            set => _tickRate = Math.Clamp(value, 0.5f, 2f);
        }

        /// <summary>创建 SoundPool 并加载节拍音与提醒音。</summary>
        public void Init(MetronomeSounds sounds)
        {
            var context = Application.Context;

            // Game 用途可与音乐混音；低延迟标志换取更快响应
            var attr = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Sonification)!
                .SetFlags(AudioFlags.LowLatency)!
                .Build();

            _soundPool = new SoundPool.Builder()
                .SetAudioAttributes(attr)!
                .SetMaxStreams(MAX_STREAMS)!
                .Build();

            if (_soundPool == null)
            {
                Debug.WriteLine("Failed to create SoundPool");
                return;
            }

            _soundPool.LoadComplete += OnLoadComplete;
            _tickSound = Load(context, sounds.TickResourceId);
            _tooSlowCue = Load(context, sounds.TooSlowCueResourceId);
            _tooFastCue = Load(context, sounds.TooFastCueResourceId);
            Debug.WriteLine($"SoundPool initialized: tick={_tickSound} slow={_tooSlowCue} fast={_tooFastCue}");
        }

        /// <summary>按每分钟步数开始播放节拍。</summary>
        public void Start(int spm)
        {
            if (spm <= 0) return;

            Stop();

            _intervalMs = (int)(60000.0 / spm);
            _nextTickTime = SystemClock.UptimeMillis();
            _running = true;

            if (Looper.MainLooper != null) _handler ??= new Handler(Looper.MainLooper);

            _tickRunnable = new Runnable(() =>
            {
                if (!_running) return;

                PlayTick();

                // 按绝对时间排程，避免累积漂移
                _nextTickTime += _intervalMs;
                var delay = Math.Max(0, _nextTickTime - SystemClock.UptimeMillis());
                _handler?.PostDelayed(_tickRunnable!, delay);
            });

            _handler?.Post(_tickRunnable);
            Debug.WriteLine($"Started cadence play at {spm} SPM");
        }

        public void Stop()
        {
            _running = false;
            if (_tickRunnable != null) _handler?.RemoveCallbacks(_tickRunnable);
        }

        /// <summary>播放一次提醒音；未加载或类型为 None 时忽略。</summary>
        public void PlayCue(CadenceAlertKind kind)
        {
            int sound = kind switch
            {
                CadenceAlertKind.TooSlow => _tooSlowCue,
                CadenceAlertKind.TooFast => _tooFastCue,
                _ => -1
            };
            Play(sound, 1f);
        }

        private void PlayTick() => Play(_tickSound, _tickRate);

        private int Load(Context context, int resourceId) =>
            resourceId > 0 && _soundPool != null ? _soundPool.Load(context, resourceId, 1) : -1;

        private void Play(int sound, float rate)
        {
            var pool = _soundPool;
            if (pool == null || sound == -1) return;
            lock (_lockObject)
            {
                if (!_loaded.Contains(sound)) return;
            }
            // 参数：音频 ID，左右声道音量，优先级，循环次数，播放速率
            pool.Play(sound, 1f, 1f, 1, 0, rate);
        }

        private void OnLoadComplete(object? sender, SoundPool.LoadCompleteEventArgs e)
        {
            if (e.Status != 0)
            {
                Debug.WriteLine($"Sound load failed: sample={e.SampleId} status={e.Status}");
                return;
            }
            lock (_lockObject)
            {
                _loaded.Add(e.SampleId);
            }
        }

        public void Dispose()
        {
            Stop();
            lock (_lockObject)
            {
                if (_soundPool != null)
                {
                    _soundPool.LoadComplete -= OnLoadComplete;
                    _soundPool.Release();
                    _soundPool = null;
                }
                _loaded.Clear();
            }
        }
    }
}

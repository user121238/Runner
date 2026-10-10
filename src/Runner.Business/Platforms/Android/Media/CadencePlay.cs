using Android.Media;
using Android.OS;
using Java.Lang;
using Runner.Business.Media;
using Application = Android.App.Application;
using Debug = System.Diagnostics.Debug;
using Math = System.Math;

namespace Runner.Business.Platforms.Android.Media
{
    /// <summary>
    /// 步频节拍播放器：用 SoundPool 与音乐混音，按绝对时间排程避免累积漂移。
    /// </summary>
    public class CadencePlay(int soundResourceId) : ICadencePlayer
    {
        private const int MAX_STREAMS = 1;

        private readonly Lock _lockObject = new();
        private SoundPool? _soundPool;
        private int _soundId = -1;
        private volatile bool _isLoaded;

        private Handler? _handler;
        private Runnable? _tickRunnable;
        private long _nextTickTime;
        private bool _running;
        private int _intervalMs;

        public event Action<string>? OnError;

        public Task InitializeAsync() => Task.Run(() => Init(soundResourceId));

        /// <summary>创建 SoundPool 并加载节拍音。</summary>
        public void Init(int soundResourceId)
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
            _soundId = _soundPool.Load(context, soundResourceId, 1);
            Debug.WriteLine($"SoundPool initialized, sound id {_soundId}");
        }

        /// <summary>按每分钟步数开始播放节拍；非正数忽略。</summary>
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

        private void PlayTick()
        {
            var pool = _soundPool;
            if (_isLoaded && pool != null && _soundId != -1)
            {
                // 参数：音频 ID，左右声道音量，优先级，循环次数，播放速率
                pool.Play(_soundId, 1f, 1f, 1, 0, 1f);
            }
        }

        private void OnLoadComplete(object? sender, SoundPool.LoadCompleteEventArgs e)
        {
            if (e.Status != 0) OnError?.Invoke($"加载节拍音失败: {e.Status}");
            if (e.SampleId == _soundId && e.Status == 0)
            {
                _isLoaded = true;
                Debug.WriteLine("Sound loaded successfully");
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
                _isLoaded = false;
            }
            OnError = null;
        }
    }
}

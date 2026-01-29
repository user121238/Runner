using _Microsoft.Android.Resource.Designer;
using Android.Media;
using Android.OS;
using Java.Lang;
using Application = Android.App.Application;
using Debug = System.Diagnostics.Debug;
using Math = System.Math;

namespace Runner.Client.Platforms.Android.Native
{
    public class CadencePlay : IDisposable
    {
        private SoundPool? _soundPool;
        private int _soundId = -1;
        private volatile bool _isLoaded = false;
        private const int MAX_STREAMS = 1;
        private readonly Lock _lockObject = new Lock();
        private Handler? _handler;
        private Runnable? _tickRunnable;
        private long _nextTickTime;
        private bool _running;
        private int _intervalMs;

        public void Init()
        {
            var context = Application.Context;

            var attr = new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Sonification)!
                .SetFlags(AudioFlags.LowLatency)!
                .Build();

            _soundPool = new SoundPool.Builder()!
                .SetAudioAttributes(attr)!
                .SetMaxStreams(MAX_STREAMS)!
                .Build();

            if (_soundPool == null)
            {
                Debug.WriteLine("Failed to create SoundPool");
                return;
            }

            _soundPool.LoadComplete += OnLoadComplete;

            _soundId = _soundPool.Load(context, ResourceConstant.Raw.beep3, 1);

            Debug.WriteLine($"Loading sound with ID: {_soundId}");

            Debug.WriteLine("SoundPool initialized");
        }

        public void Start(int spm)
        {
            if (spm <= 0) return;
            Stop();
            _intervalMs = (int)(60000.0 / spm);
            _nextTickTime = SystemClock.UptimeMillis();

            _running = true;

            _handler ??= new Handler(Looper.MainLooper);

            _tickRunnable = new Runnable(() =>
            {
                if (!_running) return;

                PlayTick();

                _nextTickTime += _intervalMs;

                var delay = Math.Max(0, _nextTickTime - SystemClock.UptimeMillis());

                _handler?.PostDelayed(_tickRunnable, delay);

            });

            _handler.Post(_tickRunnable);

            Debug.WriteLine($"Started cadence play at {spm} SPM");
        }

        public void Stop()
        {
            _running = false;
            _handler?.RemoveCallbacks(_tickRunnable);
        }

        private void PlayTick()
        {

            var pool = _soundPool;
            if (_isLoaded && pool != null && _soundId != -1)
            {

                pool.Play(_soundId, 1f, 1f, 1, 0, 1f);
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

        }

        private void OnLoadComplete(object? sender, SoundPool.LoadCompleteEventArgs e)
        {

            if (e.SampleId == _soundId && e.Status == 0)
            {
                _isLoaded = true;
                Debug.WriteLine("Sound loaded successfully");
            }
        }
    }
}

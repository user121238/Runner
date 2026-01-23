using System.Diagnostics;
using Android.Content;
using Android.Media;
using Application = Android.App.Application;

namespace Runner.Client.Platforms.Android.Native
{
    public class CadencePlay : IDisposable
    {
        private SoundPool? _soundPool;
        private int _soundId;
        private volatile bool _isLoaded = false;
        private System.Timers.Timer? _timer;

        public void Init()
        {
            var context = Application.Context;
            
            var attr = new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.Game)!
                .SetContentType(AudioContentType.Sonification)!
                .Build();

            _soundPool = new SoundPool.Builder()!
                .SetMaxStreams(1)!
                .SetAudioAttributes(attr)!
                .Build();

            _soundId = _soundPool.Load(context, Resource.Raw.beep1, 1);
            _soundPool.LoadComplete += (s, e) => _isLoaded = true;
        }

        public void Start(int spm)
        {
            Stop();
            var interval = 60000.0 / spm;
            _timer = new System.Timers.Timer(interval);
            _timer.Elapsed += (s, e) => PlayTick();
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        private void PlayTick()
        {
            if (_isLoaded)
            {
                var id = _soundPool?.Play(_soundId, 1f, 1f, 1, 0, 1f);
                Debug.WriteLine(id);
            }
        }

        public void Dispose()
        {
            Stop();
            _soundPool?.Release();
            _soundPool = null;
        }
    }
}

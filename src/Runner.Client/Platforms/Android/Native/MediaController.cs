using Android.Content;
using Android.Media;
using Android.Views;

namespace Runner.Client.Platforms.Android.Native
{
    public static class MediaController
    {

        /// <summary>
        /// 发送按钮广播
        /// </summary>
        /// <param name="keycode"></param>
        public static void SendMediaKey(Keycode keycode)
        {
            var context = Platform.CurrentActivity;

            var downEvent = new KeyEvent(KeyEventActions.Down, keycode);

            var upEvent = new KeyEvent(KeyEventActions.Up, keycode);

            if (context == null) return;

            var audioManage = context.GetSystemService(Context.AudioService) as AudioManager;

            audioManage?.DispatchMediaKeyEvent(downEvent);

            audioManage?.DispatchMediaKeyEvent(upEvent);


        }

        /// <summary>
        /// 下一首
        /// </summary>
        public static void NextTrack() => SendMediaKey(Keycode.MediaNext);


        /// <summary>
        /// 上一首
        /// </summary>
        public static void PreviousTrack() => SendMediaKey(Keycode.MediaPrevious);


        /// <summary>
        /// 暂停/播放
        /// </summary>
        public static void PlayPause() => SendMediaKey(Keycode.MediaPlayPause);



        //private static SoundPool _soundPool;
        //private static int _soundId;

        //private static bool _isLoaded = false;

        //public static void Init(Context context)
        //{

        //    var attr = new AudioAttributes.Builder()
        //        .SetUsage(AudioUsageKind.AssistanceSonification)
        //        .SetContentType(AudioContentType.Sonification)
        //        .Build();

        //    _soundPool = new SoundPool.Builder().SetMaxStreams(1).SetAudioAttributes(attr).Build();

        //    _soundId = _soundPool.Load(context, Resource.Raw.beep1, 1);

        //    _soundPool.LoadComplete += (s, e) => _isLoaded = true;

        //}

        ///// <summary>
        ///// 播放提示音
        ///// </summary>
        //public static void PlayTick()
        //{
        //    if (_isLoaded)
        //    {
        //        _soundPool.Play(_soundId, 0.5f, 0.5f, 1, 0, 1);
        //    }
        //}



    }
}

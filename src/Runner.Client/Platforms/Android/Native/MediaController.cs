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



    }
}

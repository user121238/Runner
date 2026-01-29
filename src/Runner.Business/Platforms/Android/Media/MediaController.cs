using Android.Content;
using Android.Media;
using Android.Views;

namespace Runner.Business.Platforms.Android.Media
{
    public static class MediaController
    {

        /// <summary>
        /// 发送按钮广播
        /// </summary>
        /// <param name="keycode"></param>
        public static void SendMediaKey(Context context, Keycode keycode)
        {
            var downEvent = new KeyEvent(KeyEventActions.Down, keycode);

            var upEvent = new KeyEvent(KeyEventActions.Up, keycode);

            var audioManage = context.GetSystemService(Context.AudioService) as AudioManager;

            audioManage?.DispatchMediaKeyEvent(downEvent);

            audioManage?.DispatchMediaKeyEvent(upEvent);
        }

        /// <summary>
        /// 下一首
        /// </summary>
        public static void NextTrack(Context context) => SendMediaKey(context, Keycode.MediaNext);


        /// <summary>
        /// 上一首
        /// </summary>
        public static void PreviousTrack(Context context) => SendMediaKey(context, Keycode.MediaPrevious);


        /// <summary>
        /// 暂停/播放
        /// </summary>
        public static void PlayPause(Context context) => SendMediaKey(context, Keycode.MediaPlayPause);
    }
}

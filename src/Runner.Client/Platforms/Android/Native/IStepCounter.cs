using Android.Content;

namespace Runner.Client.Platforms.Android.Native
{
    /// <summary>
    /// 计步器接口
    /// </summary>
    public interface IStepCounter : IDisposable
    {
        /// <summary>
        /// 步数变化事件
        /// </summary>
        event Action<int>? OnStepCountChanged;

        /// <summary>
        /// 步频变化事件（每分钟步数）
        /// </summary>
        event Action<double>? OnStepFrequencyChanged;

        /// <summary>
        /// 初始化
        /// </summary>
        void Init(Context context);

        /// <summary>
        /// 开始计步
        /// </summary>
        void Start();

        /// <summary>
        /// 停止计步
        /// </summary>
        void Stop();
    }
}

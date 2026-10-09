using Android.Content;

namespace Runner.Business.Platforms.Android.StepCounter
{
    /// <summary>
    /// Android 平台计步器接口 - 扩展业务接口，添加平台特定的初始化方法
    /// </summary>
    public interface IStepCounter : IDisposable
    {
        /// <summary>
        /// 初始化（Android 平台特定）
        /// </summary>
        void Init(Context context);

        /// <summary>
        /// 步数变化事件
        /// </summary>
        event Action<int>? OnStepCountChanged;

        /// <summary>
        /// 步频变化事件（每分钟步数）
        /// </summary>
        event Action<double>? OnStepFrequencyChanged;

        /// <summary>
        /// 开始计步
        /// </summary>
        void Start();

        /// <summary>
        /// 停止计步
        /// </summary>
        void Stop();

        /// <summary>重置本次运动步数及步频，不改变启动状态。</summary>
        void Reset();
    }
}

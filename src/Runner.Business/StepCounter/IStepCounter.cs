//namespace Runner.Business.StepCounter
//{
//    /// <summary>
//    /// 计步器接口 - 平台无关的业务抽象
//    /// </summary>
//    public interface IStepCounter : IDisposable
//    {
//        /// <summary>
//        /// 步数变化事件
//        /// </summary>
//        event Action<int>? OnStepCountChanged;

//        /// <summary>
//        /// 步频变化事件（每分钟步数）
//        /// </summary>
//        event Action<double>? OnStepFrequencyChanged;

//        /// <summary>
//        /// 开始计步
//        /// </summary>
//        void Start();

//        /// <summary>
//        /// 停止计步
//        /// </summary>
//        void Stop();
//    }
//}

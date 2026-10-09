using Runner.Core.Geolocation;

namespace Runner.Business.Location
{
    /// <summary>
    /// 位置追踪器接口
    /// </summary>
    public interface ILocationTracker : IDisposable
    {
        /// <summary>
        /// 位置更新事件
        /// </summary>
        event Action<GeoCoordinate>? OnLocationUpdated;

        /// <summary>
        /// 距离更新事件（米）
        /// </summary>
        event Action<double>? OnDistanceUpdated;

        /// <summary>
        /// 速度更新事件（公里/小时）
        /// </summary>
        event Action<double>? OnSpeedUpdated;

        /// <summary>
        /// 错误事件
        /// </summary>
        event Action<string>? OnError;

        /// <summary>定位质量和采样状态，不将“有定位但位移不足”误报成无数据。</summary>
        event Action<string>? OnStatusUpdated;

        /// <summary>
        /// 开始追踪
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止追踪
        /// </summary>
        void Stop();

        /// <summary>
        /// 重置距离
        /// </summary>
        void Reset();

        /// <summary>
        /// 获取总距离（米）
        /// </summary>
        double GetTotalDistanceMeters();

        /// <summary>
        /// 获取总距离（公里）
        /// </summary>
        double GetTotalDistanceKm();

        /// <summary>
        /// 获取当前速度（公里/小时）
        /// </summary>
        double GetCurrentSpeedKmh();

        /// <summary>
        /// 获取平均速度（公里/小时）
        /// </summary>
        double GetAverageSpeedKmh();

        /// <summary>
        /// 是否正在追踪
        /// </summary>
        bool IsTracking { get; }
    }
}

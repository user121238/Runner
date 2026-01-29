namespace Runner.Core.Geolocation
{
    /// <summary>
    /// 位置过滤器配置
    /// </summary>
    public class LocationFilterConfig
    {
        /// <summary>
        /// 最小距离阈值（米）- 小于此值的跳变将被忽略
        /// </summary>
        public double MinDistanceMeters { get; set; } = 5.0;

        /// <summary>
        /// 最大速度限制（公里/小时）- 超过此速度的点将被忽略
        /// </summary>
        public double MaxSpeedKmPerHour { get; set; } = 15.0;

        /// <summary>
        /// 最小时间间隔（秒）- 两个点之间的最小时间间隔
        /// </summary>
        public double MinTimeIntervalSeconds { get; set; } = 0.5;
    }

    /// <summary>
    /// 位置过滤结果
    /// </summary>
    public enum FilterResult
    {
        /// <summary>
        /// 接受此位置
        /// </summary>
        Accept,

        /// <summary>
        /// 拒绝 - 距离太小
        /// </summary>
        RejectDistanceTooSmall,

        /// <summary>
        /// 拒绝 - 速度太快
        /// </summary>
        RejectSpeedTooFast,

        /// <summary>
        /// 拒绝 - 时间间隔太短
        /// </summary>
        RejectTimeIntervalTooShort
    }

    /// <summary>
    /// 位置过滤器 - 过滤GPS跳变和异常速度
    /// </summary>
    public class LocationFilter
    {
        private readonly LocationFilterConfig _config;
        private GeoCoordinate? _lastAcceptedLocation;

        public LocationFilter(LocationFilterConfig? config = null)
        {
            _config = config ?? new LocationFilterConfig();
        }

        /// <summary>
        /// 过滤位置点
        /// </summary>
        /// <param name="location">新的位置点</param>
        /// <returns>过滤结果</returns>
        public FilterResult Filter(GeoCoordinate location)
        {
            // 第一个点总是接受
            if (_lastAcceptedLocation == null)
            {
                _lastAcceptedLocation = location;
                return FilterResult.Accept;
            }

            var lastLocation = _lastAcceptedLocation.Value;

            // 检查时间间隔
            double timeInterval = (location.Timestamp - lastLocation.Timestamp).TotalSeconds;
            if (timeInterval < _config.MinTimeIntervalSeconds)
            {
                return FilterResult.RejectTimeIntervalTooShort;
            }

            // 计算距离
            double distance = GeoCalculator.CalculateDistance(lastLocation, location);

            // 检查最小距离
            if (distance < _config.MinDistanceMeters)
            {
                return FilterResult.RejectDistanceTooSmall;
            }

            // 计算速度
            double speedMps = GeoCalculator.CalculateSpeed(lastLocation, location);
            double speedKmh = GeoCalculator.MetersPerSecondToKmPerHour(speedMps);

            // 检查最大速度
            if (speedKmh > _config.MaxSpeedKmPerHour)
            {
                return FilterResult.RejectSpeedTooFast;
            }

            // 接受此位置
            _lastAcceptedLocation = location;
            return FilterResult.Accept;
        }

        /// <summary>
        /// 重置过滤器
        /// </summary>
        public void Reset()
        {
            _lastAcceptedLocation = null;
        }

        /// <summary>
        /// 获取最后接受的位置
        /// </summary>
        public GeoCoordinate? LastAcceptedLocation => _lastAcceptedLocation;
    }
}

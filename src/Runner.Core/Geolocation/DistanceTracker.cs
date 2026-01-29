namespace Runner.Core.Geolocation
{
    /// <summary>
    /// 距离追踪器 - 累加轨迹距离
    /// </summary>
    public class DistanceTracker
    {
        private readonly LocationFilter _filter;
        private readonly List<GeoCoordinate> _trajectory = new();
        private double _totalDistance = 0;

        /// <summary>
        /// 总距离（米）
        /// </summary>
        public double TotalDistanceMeters => _totalDistance;

        /// <summary>
        /// 总距离（公里）
        /// </summary>
        public double TotalDistanceKm => _totalDistance / 1000.0;

        /// <summary>
        /// 轨迹点数量
        /// </summary>
        public int TrajectoryPointCount => _trajectory.Count;

        /// <summary>
        /// 获取轨迹副本
        /// </summary>
        public IReadOnlyList<GeoCoordinate> Trajectory => _trajectory.AsReadOnly();

        public DistanceTracker(LocationFilterConfig? filterConfig = null)
        {
            _filter = new LocationFilter(filterConfig);
        }

        /// <summary>
        /// 添加位置点
        /// </summary>
        /// <param name="location">新的位置点</param>
        /// <returns>是否接受此位置点</returns>
        public bool AddLocation(GeoCoordinate location)
        {
            return AddLocation(location, out _);
        }

        /// <summary>
        /// 添加位置点（带过滤结果）
        /// </summary>
        /// <param name="location">新的位置点</param>
        /// <param name="filterResult">过滤结果</param>
        /// <returns>是否接受此位置点</returns>
        public bool AddLocation(GeoCoordinate location, out FilterResult filterResult)
        {
            filterResult = _filter.Filter(location);

            if (filterResult == FilterResult.Accept)
            {
                // 如果不是第一个点，计算距离并累加
                if (_trajectory.Count > 0)
                {
                    var lastLocation = _trajectory[^1];
                    double distance = GeoCalculator.CalculateDistance(lastLocation, location);
                    _totalDistance += distance;
                }

                _trajectory.Add(location);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取当前速度（基于最后两个点）
        /// </summary>
        /// <returns>速度（米/秒），如果点数不足返回0</returns>
        public double GetCurrentSpeedMps()
        {
            if (_trajectory.Count < 2)
                return 0;

            var last = _trajectory[^1];
            var secondLast = _trajectory[^2];

            return GeoCalculator.CalculateSpeed(secondLast, last);
        }

        /// <summary>
        /// 获取当前速度（公里/小时）
        /// </summary>
        public double GetCurrentSpeedKmh()
        {
            return GeoCalculator.MetersPerSecondToKmPerHour(GetCurrentSpeedMps());
        }

        /// <summary>
        /// 获取平均速度（米/秒）
        /// </summary>
        public double GetAverageSpeedMps()
        {
            if (_trajectory.Count < 2)
                return 0;

            var totalTime = (_trajectory[^1].Timestamp - _trajectory[0].Timestamp).TotalSeconds;

            if (totalTime <= 0)
                return 0;

            return _totalDistance / totalTime;
        }

        /// <summary>
        /// 获取平均速度（公里/小时）
        /// </summary>
        public double GetAverageSpeedKmh()
        {
            return GeoCalculator.MetersPerSecondToKmPerHour(GetAverageSpeedMps());
        }

        /// <summary>
        /// 重置追踪器
        /// </summary>
        public void Reset()
        {
            _trajectory.Clear();
            _totalDistance = 0;
            _filter.Reset();
        }
    }
}

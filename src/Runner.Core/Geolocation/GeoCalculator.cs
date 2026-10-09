namespace Runner.Core.Geolocation
{
    /// <summary>
    /// 地理计算工具 - 计算GPS坐标之间的距离和速度
    /// </summary>
    public static class GeoCalculator
    {
        private const double EARTH_RADIUS_METERS = 6371000; // 地球半径（米）

        /// <summary>
        /// 使用 Haversine 公式计算两个GPS坐标之间的距离（米）
        /// </summary>
        public static double CalculateDistance(GeoCoordinate from, GeoCoordinate to)
        {
            if (!from.IsValid) throw new ArgumentOutOfRangeException(nameof(from));
            if (!to.IsValid) throw new ArgumentOutOfRangeException(nameof(to));
            double lat1Rad = DegreesToRadians(from.Latitude);
            double lat2Rad = DegreesToRadians(to.Latitude);
            double deltaLat = DegreesToRadians(to.Latitude - from.Latitude);
            double deltaLon = DegreesToRadians(to.Longitude - from.Longitude);

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                      Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                      Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            a = Math.Clamp(a, 0, 1);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EARTH_RADIUS_METERS * c;
        }

        /// <summary>
        /// 计算两个坐标之间的速度（米/秒）
        /// </summary>
        public static double CalculateSpeed(GeoCoordinate from, GeoCoordinate to)
        {
            double distance = CalculateDistance(from, to);
            double timeSeconds = (to.Timestamp - from.Timestamp).TotalSeconds;

            if (timeSeconds <= 0)
                return 0;

            return distance / timeSeconds;
        }

        /// <summary>
        /// 将速度从米/秒转换为公里/小时
        /// </summary>
        public static double MetersPerSecondToKmPerHour(double metersPerSecond)
        {
            return metersPerSecond * 3.6;
        }

        /// <summary>
        /// 将速度从公里/小时转换为米/秒
        /// </summary>
        public static double KmPerHourToMetersPerSecond(double kmPerHour)
        {
            return kmPerHour / 3.6;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}

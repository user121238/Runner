namespace Runner.Core.Geolocation;

/// <summary>
/// 距离使用不重叠的定位误差圈作为移动门限。精度是估计值，而非严格误差上界；
/// 缺少精度时保留最小距离门限，不能声称能区分所有静止漂移和真实移动。
/// </summary>
public class LocationFilter
{
    private readonly LocationFilterConfig _config;
    private GeoCoordinate? _lastAcceptedLocation;
    private DateTimeOffset? _lastObservationTime;

    public LocationFilter(LocationFilterConfig? config = null)
    {
        _config = (config ?? new()).Snapshot();
    }

    public GeoCoordinate? LastAcceptedLocation => _lastAcceptedLocation;

    public FilterResult Filter(GeoCoordinate location)
    {
        if (!location.IsValid) return FilterResult.RejectInvalidCoordinate;
        if (location.HorizontalAccuracyMeters > _config.MaxAccuracyMeters)
            return FilterResult.RejectAccuracyTooPoor;

        double interval = _lastObservationTime is DateTimeOffset lastTime
            ? (location.Timestamp - lastTime).TotalSeconds : double.PositiveInfinity;
        if (interval <= 0 || interval < _config.MinTimeIntervalSeconds)
            return FilterResult.RejectTimeIntervalTooShort;

        if (_lastAcceptedLocation is not GeoCoordinate last || interval > _config.MaxGapSeconds)
            return Accept(location);

        double distance = GeoCalculator.CalculateDistance(last, location);
        double elapsed = (location.Timestamp - last.Timestamp).TotalSeconds;
        if (distance / elapsed * 3.6 > _config.MaxSpeedKmPerHour)
            return FilterResult.RejectSpeedTooFast;

        double uncertainty = (last.HorizontalAccuracyMeters ?? 0) + (location.HorizontalAccuracyMeters ?? 0);
        double threshold = Math.Max(_config.MinDistanceMeters, uncertainty);
        _lastObservationTime = location.Timestamp;
        if (distance == 0 || distance < threshold)
            return FilterResult.RejectDistanceTooSmall;

        return Accept(location);
    }

    private FilterResult Accept(GeoCoordinate location)
    {
        _lastAcceptedLocation = location;
        _lastObservationTime = location.Timestamp;
        return FilterResult.Accept;
    }

    public void Reset()
    {
        _lastAcceptedLocation = null;
        _lastObservationTime = null;
    }
}

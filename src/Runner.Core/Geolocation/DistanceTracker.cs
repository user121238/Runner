namespace Runner.Core.Geolocation;

/// <summary>按连续录制段累计距离；有效观测、距离锚点和速度时效分别维护。</summary>
public class DistanceTracker
{
    private readonly LocationFilterConfig _config;
    private readonly LocationFilter _filter;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private readonly List<GeoCoordinate> _trajectory = new();
    private GeoCoordinate? _lastDistancePoint;
    private GeoCoordinate? _lastObservation;
    private double _totalDistance;
    private double _recordedSeconds;
    private double _currentSpeed;

    public DistanceTracker(LocationFilterConfig? filterConfig = null, TimeProvider? timeProvider = null)
    {
        _config = (filterConfig ?? new()).Snapshot();
        _filter = new LocationFilter(_config);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public double TotalDistanceMeters { get { lock (_lock) return _totalDistance; } }
    public double TotalDistanceKm => TotalDistanceMeters / 1000;
    public int TrajectoryPointCount { get { lock (_lock) return _trajectory.Count; } }
    // 真正的快照：调用方枚举时不受传感器回调修改影响。
    public IReadOnlyList<GeoCoordinate> Trajectory { get { lock (_lock) return _trajectory.ToArray(); } }

    public bool AddLocation(GeoCoordinate location) => AddLocation(location, out _);

    public bool AddLocation(GeoCoordinate location, out FilterResult filterResult)
    {
        lock (_lock)
        {
            double age = (_timeProvider.GetUtcNow() - location.Timestamp).TotalSeconds;
            if (age < 0)
            {
                filterResult = FilterResult.RejectFutureLocation;
                return false;
            }
            if (age > _config.MaxLocationAgeSeconds)
            {
                filterResult = FilterResult.RejectStaleLocation;
                return false;
            }

            filterResult = _filter.Filter(location);
            if (filterResult != FilterResult.Accept && filterResult != FilterResult.RejectDistanceTooSmall)
                return false;

            UpdateObservation(location);
            double calculatedSpeed = 0;
            if (filterResult == FilterResult.Accept)
            {
                if (_lastDistancePoint is GeoCoordinate last)
                {
                    double distance = GeoCalculator.CalculateDistance(last, location);
                    _totalDistance += distance;
                    calculatedSpeed = distance / (location.Timestamp - last.Timestamp).TotalSeconds;
                }
                _lastDistancePoint = location;
                _trajectory.Add(location);
            }

            double? nativeSpeed = location.SpeedMetersPerSecond;
            _currentSpeed = nativeSpeed is double speed && double.IsFinite(speed) &&
                speed >= 0 && speed * 3.6 <= _config.MaxSpeedKmPerHour ? speed : calculatedSpeed;
            return filterResult == FilterResult.Accept;
        }
    }

    private void UpdateObservation(GeoCoordinate location)
    {
        if (_lastObservation is GeoCoordinate last)
        {
            double elapsed = (location.Timestamp - last.Timestamp).TotalSeconds;
            if (elapsed > _config.MaxGapSeconds)
                _lastDistancePoint = null;
            else
                _recordedSeconds += elapsed;
        }
        _lastObservation = location;
    }

    public double GetCurrentSpeedMps()
    {
        lock (_lock)
        {
            if (_lastObservation is not GeoCoordinate last) return 0;
            double age = (_timeProvider.GetUtcNow() - last.Timestamp).TotalSeconds;
            return age < 0 || age >= _config.SpeedTimeoutSeconds ? 0 : _currentSpeed;
        }
    }

    public double GetCurrentSpeedKmh() => GeoCalculator.MetersPerSecondToKmPerHour(GetCurrentSpeedMps());

    /// <summary>平均速度包含段内静止时间，不包含显式暂停及长时间失去定位的段间空白。</summary>
    public double GetAverageSpeedMps()
    {
        lock (_lock) return _recordedSeconds > 0 ? _totalDistance / _recordedSeconds : 0;
    }

    public double GetAverageSpeedKmh() => GeoCalculator.MetersPerSecondToKmPerHour(GetAverageSpeedMps());

    /// <summary>暂停/恢复时断开轨迹，保留已录制的距离和时间。</summary>
    public void BeginSegment()
    {
        lock (_lock)
        {
            _filter.Reset();
            _lastDistancePoint = null;
            _lastObservation = null;
            _currentSpeed = 0;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            BeginSegment();
            _trajectory.Clear();
            _totalDistance = 0;
            _recordedSeconds = 0;
        }
    }
}

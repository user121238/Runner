using CoreLocation;
using Foundation;
using Runner.Business.Diagnostics;
using Runner.Business.Location;
using Runner.Core.Geolocation;

namespace Runner.Business.Platforms.iOS.Location;

/// <summary>用户主动开始的前/后台定位；距离过滤与 Android 共用 DistanceTracker。</summary>
public sealed class IosLocationTracker : ILocationTracker
{
    private readonly TimeProvider _clock;
    private readonly DistanceTracker _distanceTracker;
    private readonly CLLocationManager _manager;
    private readonly LocationListener _listener;
    private Timer? _speedTimer;
    private DateTimeOffset _segmentStartTime;
    private volatile bool _isTracking;
    private bool _disposed;

    public IosLocationTracker(TimeProvider clock)
    {
        _clock = clock;
        _distanceTracker = new DistanceTracker(timeProvider: clock);
        _listener = new LocationListener(this);
        _manager = new CLLocationManager
        {
            Delegate = _listener,
            DesiredAccuracy = CLLocation.AccuracyBest,
            DistanceFilter = CLLocationDistance.FilterNone,
            ActivityType = CLActivityType.Fitness,
            PausesLocationUpdatesAutomatically = false,
            ShowsBackgroundLocationIndicator = true
        };
    }

    public event Action<GeoCoordinate>? OnLocationUpdated;
    public event Action<double>? OnDistanceUpdated;
    public event Action<double>? OnSpeedUpdated;
    public event Action<string>? OnError;
    public event Action<string>? OnStatusUpdated;
    public bool IsTracking => _isTracking;

    public Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isTracking) return Task.CompletedTask;
        if (!CLLocationManager.LocationServicesEnabled)
            throw new InvalidOperationException("定位服务未启用，请在设置中开启定位");
        _distanceTracker.BeginSegment();
        _segmentStartTime = _clock.GetUtcNow();
        _manager.AllowsBackgroundLocationUpdates = true;
        _manager.StartUpdatingLocation();
        _isTracking = true;
        _speedTimer = new Timer(_ => RefreshSpeed(), null, 1000, 1000);
        DiagnosticLog.Write("GPS", $"开始定位 授权={_manager.AuthorizationStatus} 精度授权={_manager.AccuracyAuthorization}");
        OnStatusUpdated?.Invoke("正在定位，请到开阔处");
        return Task.CompletedTask;
    }

    public void Stop()
    {
        bool wasTracking = _isTracking;
        _isTracking = false;
        _speedTimer?.Dispose();
        _speedTimer = null;
        _manager.StopUpdatingLocation();
        _manager.AllowsBackgroundLocationUpdates = false;
        _distanceTracker.BeginSegment();
        if (wasTracking)
            DiagnosticLog.Write("GPS", $"停止定位 累计={_distanceTracker.TotalDistanceMeters:F1}m 轨迹点={_distanceTracker.TrajectoryPointCount}");
        OnSpeedUpdated?.Invoke(0);
        OnStatusUpdated?.Invoke("已停止");
    }

    public void Reset()
    {
        _distanceTracker.Reset();
        OnDistanceUpdated?.Invoke(0);
        OnSpeedUpdated?.Invoke(0);
    }

    private void Receive(CLLocation location)
    {
        if (!_isTracking) return;
        try
        {
            var wallTime = DateTimeOffset.UnixEpoch.AddSeconds(location.Timestamp.SecondsSince1970);
            var age = DateTimeOffset.UtcNow - wallTime;
            var sampleTime = _clock.GetUtcNow() - age;
            // 在单调时钟上判定分段，避免用户调整墙钟后拒绝后续所有位置。
            if (sampleTime < _segmentStartTime)
            {
                DiagnosticLog.Write("GPS", $"忽略早于本段的缓存点 龄={age.TotalSeconds:F1}s ±{location.HorizontalAccuracy:F1}m");
                return;
            }
            var point = new GeoCoordinate(location.Coordinate.Latitude, location.Coordinate.Longitude,
                location.Altitude, sampleTime,
                location.HorizontalAccuracy >= 0 ? location.HorizontalAccuracy : double.PositiveInfinity,
                location.Speed >= 0 ? location.Speed : null);
            var anchor = _distanceTracker.LastAcceptedLocation;
            bool accepted = _distanceTracker.AddLocation(point, out var result);
            string anchorDistance = anchor is GeoCoordinate a ? GeoCalculator.CalculateDistance(a, point).ToString("F1") : "-";
            DiagnosticLog.Write("GPS", $"{result} ±{location.HorizontalAccuracy:F1}m v={location.Speed:F1}m/s 龄={age.TotalSeconds:F1}s 距锚点={anchorDistance}m 累计={_distanceTracker.TotalDistanceMeters:F1}m 点={_distanceTracker.TrajectoryPointCount} ({location.Coordinate.Latitude:F5},{location.Coordinate.Longitude:F5})");
            OnStatusUpdated?.Invoke(result switch
            {
                FilterResult.Accept => $"GPS有效 ±{location.HorizontalAccuracy:F1}米 · {_distanceTracker.TrajectoryPointCount}个轨迹点",
                FilterResult.RejectDistanceTooSmall => $"GPS有效 ±{location.HorizontalAccuracy:F1}米 · 位移未超过精度门限",
                FilterResult.RejectAccuracyTooPoor => "定位较弱，请检查精确位置权限或到开阔处",
                _ => "正在等待有效定位"
            });
            if (accepted)
            {
                OnLocationUpdated?.Invoke(point);
                OnDistanceUpdated?.Invoke(_distanceTracker.TotalDistanceMeters);
            }
            RefreshSpeed();
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"处理位置失败: {ex.Message}");
        }
    }

    private void RefreshSpeed()
    {
        if (_isTracking) OnSpeedUpdated?.Invoke(_distanceTracker.GetCurrentSpeedKmh());
    }

    public double GetTotalDistanceMeters() => _distanceTracker.TotalDistanceMeters;
    public double GetTotalDistanceKm() => _distanceTracker.TotalDistanceKm;
    public double GetCurrentSpeedKmh() => _distanceTracker.GetCurrentSpeedKmh();
    public double GetAverageSpeedKmh() => _distanceTracker.GetAverageSpeedKmh();

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        _manager.WeakDelegate = null;
        _manager.Dispose();
        _listener.Dispose();
        OnLocationUpdated = null;
        OnDistanceUpdated = null;
        OnSpeedUpdated = null;
        OnError = null;
        OnStatusUpdated = null;
    }

    private sealed class LocationListener(IosLocationTracker owner) : CLLocationManagerDelegate
    {
        public override void LocationsUpdated(CLLocationManager manager, CLLocation[] locations)
        {
            foreach (var location in locations) owner.Receive(location);
        }

        public override void Failed(CLLocationManager manager, NSError error)
        {
            DiagnosticLog.Write("GPS", $"CLLocationManager 失败 code={error.Code} {error.LocalizedDescription}");
            if (owner._isTracking) owner.OnError?.Invoke($"定位失败: {error.LocalizedDescription}");
        }

        public override void DidChangeAuthorization(CLLocationManager manager) =>
            DiagnosticLog.Write("GPS", $"授权变化 {manager.AuthorizationStatus} 精度={manager.AccuracyAuthorization}");
    }
}

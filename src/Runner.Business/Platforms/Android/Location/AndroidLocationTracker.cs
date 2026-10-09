using Android.Content;
using Android.Locations;
using Android.OS;
using Runner.Business.Location;
using Runner.Core.Geolocation;
using Runner.Core.SignalProcessing;
using Debug = System.Diagnostics.Debug;

namespace Runner.Business.Platforms.Android.Location;

public class AndroidLocationTracker : Java.Lang.Object, ILocationTracker, ILocationListener
{
    private readonly Context _context;
    private LocationManager? _locationManager;
    private readonly DistanceTracker _distanceTracker;
    private readonly SensorClock _clock = new(SystemClock.ElapsedRealtimeNanos);
    private Timer? _speedTimer;
    private volatile bool _isTracking;

    public event Action<GeoCoordinate>? OnLocationUpdated;
    public event Action<double>? OnDistanceUpdated;
    public event Action<double>? OnSpeedUpdated;
    public event Action<string>? OnError;
    public event Action<string>? OnStatusUpdated;
    public bool IsTracking => _isTracking;

    public AndroidLocationTracker(Context context, LocationFilterConfig? filterConfig = null)
    {
        _context = context.ApplicationContext ?? context;
        _distanceTracker = new DistanceTracker(filterConfig, _clock);
    }

    public Task StartAsync()
    {
        if (_isTracking) return Task.CompletedTask;
        _locationManager = (LocationManager?)_context.GetSystemService(Context.LocationService)
            ?? throw new InvalidOperationException("获取位置服务失败");
        if (!_locationManager.IsProviderEnabled(LocationManager.GpsProvider))
            throw new InvalidOperationException("GPS未启用，请在设置中开启GPS");

        _distanceTracker.BeginSegment();
        _locationManager.RequestLocationUpdates(LocationManager.GpsProvider, 1000, 0, this, Looper.MainLooper);
        _isTracking = true;
        _speedTimer = new Timer(UpdateSpeed, null, 1000, 1000);
        OnStatusUpdated?.Invoke("正在定位，请到开阔处");
        Debug.WriteLine("[Runner.Gps] tracking started");
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _isTracking = false;
        _speedTimer?.Dispose();
        _speedTimer = null;
        _locationManager?.RemoveUpdates(this);
        _distanceTracker.BeginSegment();
        OnSpeedUpdated?.Invoke(0);
        OnStatusUpdated?.Invoke("已停止");
        Debug.WriteLine("[Runner.Gps] tracking stopped; segment closed");
    }

    public void Reset()
    {
        _distanceTracker.Reset();
        OnDistanceUpdated?.Invoke(0);
        OnSpeedUpdated?.Invoke(0);
        Debug.WriteLine("[Runner.Gps] distance reset");
    }

    public double GetTotalDistanceMeters() => _distanceTracker.TotalDistanceMeters;
    public double GetTotalDistanceKm() => _distanceTracker.TotalDistanceKm;
    public double GetCurrentSpeedKmh() => _distanceTracker.GetCurrentSpeedKmh();
    public double GetAverageSpeedKmh() => _distanceTracker.GetAverageSpeedKmh();

    public void OnLocationChanged(global::Android.Locations.Location location)
    {
        if (!_isTracking) return;
        try
        {
            var coordinate = new GeoCoordinate(location.Latitude, location.Longitude, location.Altitude,
                _clock.GetSampleTime(location.ElapsedRealtimeNanos),
                location.HasAccuracy ? location.Accuracy : double.PositiveInfinity,
                location.HasSpeed ? location.Speed : null);
            bool accepted = _distanceTracker.AddLocation(coordinate, out var result);
            string status = result switch
            {
                FilterResult.Accept => $"GPS有效 ±{location.Accuracy:F1}米 · {_distanceTracker.TrajectoryPointCount}个轨迹点",
                FilterResult.RejectDistanceTooSmall => $"GPS有效 ±{location.Accuracy:F1}米 · 位移未超过精度门限",
                FilterResult.RejectAccuracyTooPoor => $"定位较弱 ±{location.Accuracy:F0}米 · 暂不计入距离",
                FilterResult.RejectStaleLocation => "正在定位 · 已忽略过期采样",
                FilterResult.RejectFutureLocation => "定位时间异常 · 已忽略采样",
                FilterResult.RejectSpeedTooFast => "GPS异常跳点 · 暂不计入距离",
                _ => "正在等待有效定位"
            };
            OnStatusUpdated?.Invoke(status);
            // 诊断只记录精度和算法结果，不记录用户经纬度。
            Debug.WriteLine($"[Runner.Gps] result={result} accuracy={location.Accuracy:F1}m ageMs={(_clock.GetUtcNow() - coordinate.Timestamp).TotalMilliseconds:F0} distance={_distanceTracker.TotalDistanceMeters:F1}m speed={_distanceTracker.GetCurrentSpeedKmh():F1}km/h");
            if (accepted)
            {
                OnLocationUpdated?.Invoke(coordinate);
                OnDistanceUpdated?.Invoke(_distanceTracker.TotalDistanceMeters);
            }
            OnSpeedUpdated?.Invoke(_distanceTracker.GetCurrentSpeedKmh());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Runner.Gps] update failed: {ex}");
            OnError?.Invoke($"处理位置失败: {ex.Message}");
        }
    }

    private void UpdateSpeed(object? state)
    {
        if (!_isTracking) return;
        // 无新定位时也刷新 UI，使过期的速度在三秒后归零。
        OnSpeedUpdated?.Invoke(_distanceTracker.GetCurrentSpeedKmh());
    }

    public void OnProviderDisabled(string provider)
    {
        if (provider == LocationManager.GpsProvider) OnError?.Invoke("GPS已被禁用");
    }

    public void OnProviderEnabled(string provider) { }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }

    public new void Dispose()
    {
        Stop();
        _locationManager = null;
        OnLocationUpdated = null;
        OnDistanceUpdated = null;
        OnSpeedUpdated = null;
        OnError = null;
        OnStatusUpdated = null;
        base.Dispose();
    }
}

using Android.Content;
using Android.Locations;
using Android.OS;
using Runner.Business.Location;
using Runner.Core.Geolocation;
using Debug = System.Diagnostics.Debug;

namespace Runner.Business.Platforms.Android.Location
{
    /// <summary>
    /// Android 位置追踪器实现
    /// 使用高频GPS（1秒更新）进行距离测算
    /// </summary>
    public class AndroidLocationTracker : Java.Lang.Object, ILocationTracker, ILocationListener
    {
        private readonly Context _context;
        private LocationManager? _locationManager;
        private readonly DistanceTracker _distanceTracker;
        private bool _isTracking;

        // GPS 更新配置
        private const long MIN_TIME_MS = 1000; // 1秒更新
        private const float MIN_DISTANCE_METERS = 0; // 不限制最小距离，让过滤器处理

        public event Action<GeoCoordinate>? OnLocationUpdated;
        public event Action<double>? OnDistanceUpdated;
        public event Action<double>? OnSpeedUpdated;
        public event Action<string>? OnError;

        public bool IsTracking => _isTracking;

        public AndroidLocationTracker(Context context, LocationFilterConfig? filterConfig = null)
        {
            _context = context;
            _distanceTracker = new DistanceTracker(filterConfig ?? new LocationFilterConfig
            {
                MinDistanceMeters = 5.0,      // 最小5米
                MaxSpeedKmPerHour = 15.0,     // 最大15公里/小时（跑步速度）
                MinTimeIntervalSeconds = 0.5   // 最小0.5秒间隔
            });
        }

        public async Task StartAsync()
        {
            if (_isTracking)
            {
                Debug.WriteLine("[AndroidLocationTracker] 已经在追踪中");
                return;
            }

            try
            {
                Debug.WriteLine("[AndroidLocationTracker] 开始位置追踪");

                _locationManager = (LocationManager?)_context.GetSystemService(Context.LocationService);

                if (_locationManager == null)
                {
                    throw new InvalidOperationException("无法获取 LocationManager");
                }

                // 检查GPS是否可用
                bool gpsEnabled = _locationManager.IsProviderEnabled(LocationManager.GpsProvider);
                Debug.WriteLine($"[AndroidLocationTracker] GPS 状态: {(gpsEnabled ? "已启用" : "未启用")}");

                if (!gpsEnabled)
                {
                    OnError?.Invoke("GPS未启用，请在设置中开启GPS");
                    return;
                }

                // 请求位置更新
                _locationManager.RequestLocationUpdates(
                    LocationManager.GpsProvider,
                    MIN_TIME_MS,
                    MIN_DISTANCE_METERS,
                    this
                );

                _isTracking = true;
                Debug.WriteLine("[AndroidLocationTracker] 位置追踪已启动");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AndroidLocationTracker] 启动失败: {ex}");
                OnError?.Invoke($"启动失败: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            if (!_isTracking)
            {
                Debug.WriteLine("[AndroidLocationTracker] 未在追踪中");
                return;
            }

            try
            {
                Debug.WriteLine("[AndroidLocationTracker] 停止位置追踪");

                _locationManager?.RemoveUpdates(this);
                _isTracking = false;

                Debug.WriteLine("[AndroidLocationTracker] 位置追踪已停止");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AndroidLocationTracker] 停止失败: {ex}");
                OnError?.Invoke($"停止失败: {ex.Message}");
            }
        }

        public void Reset()
        {
            Debug.WriteLine("[AndroidLocationTracker] 重置距离追踪");
            _distanceTracker.Reset();
            OnDistanceUpdated?.Invoke(0);
            OnSpeedUpdated?.Invoke(0);
        }

        public double GetTotalDistanceMeters() => _distanceTracker.TotalDistanceMeters;

        public double GetTotalDistanceKm() => _distanceTracker.TotalDistanceKm;

        public double GetCurrentSpeedKmh() => _distanceTracker.GetCurrentSpeedKmh();

        public double GetAverageSpeedKmh() => _distanceTracker.GetAverageSpeedKmh();

        // ILocationListener 实现
        public void OnLocationChanged(global::Android.Locations.Location location)
        {
            try
            {
                // 转换为 GeoCoordinate
                var coordinate = new GeoCoordinate(
                    location.Latitude,
                    location.Longitude,
                    location.Altitude,
                    DateTimeOffset.FromUnixTimeMilliseconds(location.Time)
                );

                Debug.WriteLine($"[AndroidLocationTracker] 收到位置: Lat={coordinate.Latitude:F6}, Lon={coordinate.Longitude:F6}, Accuracy={location.Accuracy:F1}m");

                // 添加到距离追踪器（会自动过滤）
                bool accepted = _distanceTracker.AddLocation(coordinate, out var filterResult);

                if (accepted)
                {
                    Debug.WriteLine($"[AndroidLocationTracker] ✓ 位置已接受 - 总距离: {_distanceTracker.TotalDistanceMeters:F1}m, 当前速度: {_distanceTracker.GetCurrentSpeedKmh():F1}km/h");

                    // 触发事件
                    OnLocationUpdated?.Invoke(coordinate);
                    OnDistanceUpdated?.Invoke(_distanceTracker.TotalDistanceMeters);
                    OnSpeedUpdated?.Invoke(_distanceTracker.GetCurrentSpeedKmh());
                }
                else
                {
                    // 输出详细的拒绝原因
                    string reason = filterResult switch
                    {
                        FilterResult.RejectDistanceTooSmall => "距离太小（GPS漂移）",
                        FilterResult.RejectSpeedTooFast => "速度太快（GPS跳变）",
                        FilterResult.RejectTimeIntervalTooShort => "时间间隔太短",
                        _ => "未知原因"
                    };
                    Debug.WriteLine($"[AndroidLocationTracker] ✗ 位置被拒绝 - 原因: {reason}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AndroidLocationTracker] 处理位置更新失败: {ex}");
                OnError?.Invoke($"处理位置失败: {ex.Message}");
            }
        }

        public void OnProviderDisabled(string provider)
        {
            Debug.WriteLine($"[AndroidLocationTracker] 提供者已禁用: {provider}");
            if (provider == LocationManager.GpsProvider)
            {
                OnError?.Invoke("GPS已被禁用");
            }
        }

        public void OnProviderEnabled(string provider)
        {
            Debug.WriteLine($"[AndroidLocationTracker] 提供者已启用: {provider}");
        }

        public void OnStatusChanged(string? provider, Availability status, Bundle? extras)
        {
            Debug.WriteLine($"[AndroidLocationTracker] 状态变化: {provider} - {status}");
        }

        public new void Dispose()
        {
            Stop();
            _locationManager = null;
            OnLocationUpdated = null;
            OnDistanceUpdated = null;
            OnSpeedUpdated = null;
            OnError = null;
            base.Dispose();
        }
    }
}

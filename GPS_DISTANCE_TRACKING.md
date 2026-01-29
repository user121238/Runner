# GPS 距离测算系统

## 概述

基于高频GPS（1秒更新）的距离测算系统，具有以下特性：

- ✅ 1秒高频GPS更新
- ✅ 滤除小于一定值的跳变（默认5米）
- ✅ 限速过滤（默认最大15公里/小时，适合跑步）
- ✅ 轨迹累加和速度计算
- ✅ 完全分层架构（Core算法 + Business业务）

## 架构设计

### Core 层（纯算法，平台无关）

#### GeoCalculator
GPS坐标计算工具：
- `CalculateDistance()` - 使用Haversine公式计算两点距离
- `CalculateSpeed()` - 计算速度
- 单位转换（米/秒 ↔ 公里/小时）

#### LocationFilter
位置过滤器，过滤GPS跳变和异常速度：
- **最小距离过滤**：小于阈值的跳变被忽略（默认5米）
- **最大速度过滤**：超过合理速度的点被忽略（默认15公里/小时）
- **最小时间间隔**：防止过于频繁的更新（默认0.5秒）

#### DistanceTracker
距离追踪器，累加轨迹距离：
- 自动过滤异常点
- 累加总距离
- 计算当前速度和平均速度
- 保存完整轨迹

### Business 层（业务逻辑）

#### ILocationTracker
位置追踪接口，定义事件和方法：
- `OnLocationUpdated` - 位置更新事件
- `OnDistanceUpdated` - 距离更新事件
- `OnSpeedUpdated` - 速度更新事件
- `OnError` - 错误事件

#### AndroidLocationTracker
Android平台实现：
- 使用 `LocationManager` 获取GPS位置
- 1秒高频更新
- 自动过滤和累加距离

## 使用方法

### 1. 配置过滤器（可选）

```csharp
var filterConfig = new LocationFilterConfig
{
    MinDistanceMeters = 5.0,        // 最小5米（小于此值的跳变被忽略）
    MaxSpeedKmPerHour = 15.0,       // 最大15公里/小时（跑步速度）
    MinTimeIntervalSeconds = 0.5    // 最小0.5秒间隔
};
```

### 2. 创建追踪器

```csharp
// 使用默认配置
var tracker = new AndroidLocationTracker(context);

// 或使用自定义配置
var tracker = new AndroidLocationTracker(context, filterConfig);
```

### 3. 订阅事件

```csharp
tracker.OnLocationUpdated += (coordinate) =>
{
    Debug.WriteLine($"位置: {coordinate.Latitude}, {coordinate.Longitude}");
};

tracker.OnDistanceUpdated += (distanceMeters) =>
{
    Debug.WriteLine($"总距离: {distanceMeters:F1} 米");
};

tracker.OnSpeedUpdated += (speedKmh) =>
{
    Debug.WriteLine($"当前速度: {speedKmh:F1} 公里/小时");
};

tracker.OnError += (error) =>
{
    Debug.WriteLine($"错误: {error}");
};
```

### 4. 请求权限

```csharp
var status = await Permissions.CheckStatusAsync<LocationPermission>();
if (status != PermissionStatus.Granted)
{
    status = await Permissions.RequestAsync<LocationPermission>();
}

if (status == PermissionStatus.Granted)
{
    await tracker.StartAsync();
}
```

### 5. 开始追踪

```csharp
await tracker.StartAsync();
```

### 6. 获取数据

```csharp
// 获取总距离
double distanceMeters = tracker.GetTotalDistanceMeters();
double distanceKm = tracker.GetTotalDistanceKm();

// 获取速度
double currentSpeed = tracker.GetCurrentSpeedKmh();
double averageSpeed = tracker.GetAverageSpeedKmh();

// 检查状态
bool isTracking = tracker.IsTracking;
```

### 7. 停止和重置

```csharp
// 停止追踪
tracker.Stop();

// 重置距离（清空轨迹）
tracker.Reset();

// 释放资源
tracker.Dispose();
```

## 完整示例（ViewModel）

```csharp
public class RunningViewModel : BaseViewModel
{
    private ILocationTracker? _locationTracker;
    private double _distance;
    private double _speed;

    public double Distance
    {
        get => _distance;
        set => SetProperty(ref _distance, value);
    }

    public double Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    public async Task InitializeAsync()
    {
        // 请求权限
        var status = await Permissions.CheckStatusAsync<LocationPermission>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<LocationPermission>();
        }

        if (status != PermissionStatus.Granted)
        {
            // 权限被拒绝
            return;
        }

        // 创建追踪器
        var context = Platform.CurrentActivity!;
        _locationTracker = new AndroidLocationTracker(context);

        // 订阅事件
        _locationTracker.OnDistanceUpdated += (distance) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Distance = distance / 1000.0; // 转换为公里
            });
        };

        _locationTracker.OnSpeedUpdated += (speed) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Speed = speed;
            });
        };

        _locationTracker.OnError += (error) =>
        {
            Debug.WriteLine($"GPS错误: {error}");
        };
    }

    [RelayCommand]
    private async Task StartTracking()
    {
        if (_locationTracker != null)
        {
            await _locationTracker.StartAsync();
        }
    }

    [RelayCommand]
    private void StopTracking()
    {
        _locationTracker?.Stop();
    }

    [RelayCommand]
    private void ResetDistance()
    {
        _locationTracker?.Reset();
        Distance = 0;
        Speed = 0;
    }

    protected override void Dispose(bool disposing)
    {
        _locationTracker?.Dispose();
        base.Dispose(disposing);
    }
}
```

## 配置建议

### 跑步场景
```csharp
new LocationFilterConfig
{
    MinDistanceMeters = 5.0,        // 5米
    MaxSpeedKmPerHour = 15.0,       // 15公里/小时
    MinTimeIntervalSeconds = 0.5    // 0.5秒
};
```

### 步行场景
```csharp
new LocationFilterConfig
{
    MinDistanceMeters = 3.0,        // 3米（更敏感）
    MaxSpeedKmPerHour = 8.0,        // 8公里/小时
    MinTimeIntervalSeconds = 0.5    // 0.5秒
};
```

### 骑行场景
```csharp
new LocationFilterConfig
{
    MinDistanceMeters = 10.0,       // 10米
    MaxSpeedKmPerHour = 30.0,       // 30公里/小时
    MinTimeIntervalSeconds = 1.0    // 1秒
};
```

## 技术细节

### Haversine 公式
用于计算地球表面两点之间的最短距离（大圆距离）：

```
a = sin²(Δlat/2) + cos(lat1) × cos(lat2) × sin²(Δlon/2)
c = 2 × atan2(√a, √(1−a))
d = R × c
```

其中：
- R = 地球半径（6371000米）
- Δlat = lat2 - lat1
- Δlon = lon2 - lon1

### 过滤逻辑

1. **第一个点**：总是接受
2. **时间间隔检查**：如果距离上次接受的点时间太短，拒绝
3. **距离检查**：如果距离太小（GPS漂移），拒绝
4. **速度检查**：如果速度超过合理范围（GPS跳变），拒绝
5. **接受点**：更新最后接受的点，累加距离

### 精度考虑

- GPS精度通常在5-10米
- 建筑物、树木会影响GPS信号
- 建议在开阔地带使用
- 可以通过 `location.Accuracy` 获取精度信息

## 注意事项

1. **权限**：需要 `ACCESS_FINE_LOCATION` 权限
2. **GPS开启**：用户需要在设置中开启GPS
3. **电池消耗**：高频GPS会消耗较多电量
4. **后台运行**：如需后台运行，需要配置前台服务
5. **精度**：GPS精度受环境影响，室内精度较差

## 后续优化

1. **卡尔曼滤波**：进一步平滑GPS轨迹
2. **地图匹配**：将GPS点匹配到道路
3. **海拔变化**：考虑海拔变化对距离的影响
4. **轨迹简化**：使用Douglas-Peucker算法简化轨迹
5. **离线存储**：保存轨迹到本地数据库

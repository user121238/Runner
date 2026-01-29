# GPS 过滤器调试指南

## 问题：位置被过滤器拒绝

当你看到 `[AndroidLocationTracker] ✗ 位置被拒绝` 的日志时，说明过滤器正在工作，拒绝了不合理的GPS点。

## 拒绝原因详解

### 1. 距离太小（GPS漂移）
```
✗ 位置被拒绝 - 原因: 距离太小（GPS漂移）
```

**原因：**
- 两个GPS点之间的距离小于 `MinDistanceMeters`（默认5米）
- 这通常是GPS信号漂移导致的

**何时发生：**
- 原地不动或移动很慢时
- GPS精度较差时（室内、建筑物遮挡）

**解决方案：**
- 这是正常的！原地不动不应该累加距离
- 如果你在移动但被拒绝，降低 `MinDistanceMeters` 到 2-3米

### 2. 速度太快（GPS跳变）
```
✗ 位置被拒绝 - 原因: 速度太快（GPS跳变）
```

**原因：**
- 计算出的速度超过 `MaxSpeedKmPerHour`（默认15公里/小时）
- 这通常是GPS信号跳变导致的

**何时发生：**
- GPS信号突然跳到远处的点
- 隧道、高楼等遮挡后GPS重新定位

**解决方案：**
- 这是正常的！防止异常跳变累加到距离
- 如果你在骑车，提高 `MaxSpeedKmPerHour` 到 25-30公里/小时

### 3. 时间间隔太短
```
✗ 位置被拒绝 - 原因: 时间间隔太短
```

**原因：**
- 距离上次接受的点时间小于 `MinTimeIntervalSeconds`（默认0.5秒）

**何时发生：**
- GPS更新频率过高
- 系统时间异常

**解决方案：**
- 通常不会发生（我们设置的是1秒更新）
- 如果频繁发生，检查系统时间

## 当前配置（测试模式）

我已经为你设置了更宽松的测试配置：

```csharp
MinDistanceMeters = 2.0        // 2米（更敏感）
MaxSpeedKmPerHour = 20.0       // 20公里/小时（更宽松）
MinTimeIntervalSeconds = 0.3   // 0.3秒（更频繁）
```

## 调试步骤

### 1. 查看完整日志

在 Android Studio 的 Logcat 中过滤 `AndroidLocationTracker`，你会看到：

```
[AndroidLocationTracker] 收到位置: Lat=39.123456, Lon=116.123456, Accuracy=10.5m
[AndroidLocationTracker] ✓ 位置已接受 - 总距离: 15.3m, 当前速度: 5.2km/h
```

或

```
[AndroidLocationTracker] 收到位置: Lat=39.123456, Lon=116.123456, Accuracy=10.5m
[AndroidLocationTracker] ✗ 位置被拒绝 - 原因: 距离太小（GPS漂移）
```

### 2. 理解日志信息

**接受的位置：**
- `Lat/Lon`: GPS坐标
- `Accuracy`: GPS精度（米），越小越好
- `总距离`: 累计距离
- `当前速度`: 基于最后两个点计算的速度

**拒绝的位置：**
- 会显示具体拒绝原因
- 不会累加到总距离

### 3. 测试场景

#### 场景1：原地不动
**预期：**
- 收到GPS更新
- 大部分被拒绝（距离太小）
- 总距离不增加或增加很少

**结果：** ✅ 正常

#### 场景2：慢速步行（3-5公里/小时）
**预期：**
- 收到GPS更新
- 部分被接受（移动距离>2米）
- 总距离逐渐增加

**结果：** ✅ 正常

#### 场景3：快速跑步（10-15公里/小时）
**预期：**
- 收到GPS更新
- 大部分被接受
- 总距离快速增加

**结果：** ✅ 正常

#### 场景4：GPS信号差（室内、高楼）
**预期：**
- 收到GPS更新
- 很多被拒绝（距离太小或速度太快）
- 总距离增加不稳定

**结果：** ✅ 正常（过滤器在保护数据质量）

## 配置调整建议

### 室内测试（GPS信号差）
```csharp
MinDistanceMeters = 1.0         // 1米（非常敏感）
MaxSpeedKmPerHour = 30.0        // 30公里/小时（非常宽松）
MinTimeIntervalSeconds = 0.2    // 0.2秒
```

### 步行（3-6公里/小时）
```csharp
MinDistanceMeters = 3.0         // 3米
MaxSpeedKmPerHour = 8.0         // 8公里/小时
MinTimeIntervalSeconds = 0.5    // 0.5秒
```

### 跑步（8-15公里/小时）
```csharp
MinDistanceMeters = 5.0         // 5米
MaxSpeedKmPerHour = 18.0        // 18公里/小时
MinTimeIntervalSeconds = 0.5    // 0.5秒
```

### 骑行（15-30公里/小时）
```csharp
MinDistanceMeters = 10.0        // 10米
MaxSpeedKmPerHour = 35.0        // 35公里/小时
MinTimeIntervalSeconds = 1.0    // 1秒
```

## 常见问题

### Q: 为什么原地不动还有距离增加？
A: GPS精度问题。如果增加很少（<10米/分钟），这是正常的GPS漂移。可以提高 `MinDistanceMeters` 到 5-8米。

### Q: 为什么移动了但距离不增加？
A:
1. 检查GPS是否开启
2. 检查是否在室内（GPS信号差）
3. 降低 `MinDistanceMeters` 到 2-3米
4. 查看日志，确认是否收到GPS更新

### Q: 为什么速度显示为0？
A: 需要至少2个有效点才能计算速度。继续移动，等待第二个点被接受。

### Q: 为什么距离突然跳变？
A: GPS信号跳变。过滤器应该拒绝这些点。如果没有拒绝，降低 `MaxSpeedKmPerHour`。

## 优化建议

### 1. 根据GPS精度动态调整
```csharp
// 如果GPS精度差，放宽过滤条件
if (location.Accuracy > 20) // 精度>20米
{
    MinDistanceMeters = location.Accuracy / 2;
}
```

### 2. 根据运动状态调整
```csharp
// 检测到静止状态
if (speed < 1.0) // 速度<1公里/小时
{
    MinDistanceMeters = 5.0; // 提高阈值
}
```

### 3. 添加精度过滤
```csharp
// 拒绝精度太差的点
if (location.Accuracy > 50) // 精度>50米
{
    return; // 不处理
}
```

## 总结

**过滤器被拒绝是正常的！** 它在保护你的数据质量，防止：
- GPS漂移累加到距离
- GPS跳变导致距离异常
- 不合理的速度数据

如果你发现过滤太严格，可以调整配置参数。当前的测试配置已经比较宽松了。

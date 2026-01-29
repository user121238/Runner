# 代码重构总结

## 重构目标
将平台无关的通用算法和业务抽象从 Client 层分离到 Core 和 Business 层，实现清晰的分层架构。

## 文件移动

### Runner.Core.SignalProcessing（平台无关的信号处理算法）
- **LowPassFilter.cs** - 低通滤波器
  - 用于平滑信号，去除高频噪声
  - 可用于任何需要信号平滑的场景
  
- **PeakDetector.cs** - 峰值检测器
  - 检测信号中的峰值（上升超过阈值，然后下降）
  - 可用于步态检测、心率检测、震动检测等
  
- **FrequencyCalculator.cs** - 频率计算器（原 StepFrequencyCalculator）
  - 基于时间窗口计算事件频率
  - 通用化命名：AddEvent() / GetFrequency()
  - 可用于任何需要频率统计的场景

### Runner.Business.StepCounter（业务抽象层）
- **IStepCounter.cs** - 平台无关的计步器业务接口
  - 定义了 OnStepCountChanged、OnStepFrequencyChanged 事件
  - 定义了 Start()、Stop()、Dispose() 方法
  - 移除了平台特定的 Init(Context) 方法

### Runner.Client.Platforms.Android.Native（Android 平台实现）
保留的文件：
- **IStepCounter.cs** - Android 平台接口（继承业务接口）
  - 扩展业务接口，添加 Init(Context context) 方法
  
- **AccelerometerStepCounter.cs** - 加速度计步器实现
  - 使用 LowPassFilter、PeakDetector、FrequencyCalculator
  
- **HardwareStepCounter.cs** - 硬件计步器实现
  - 使用 FrequencyCalculator
  
- **StepCounterFactory.cs** - 工厂类
  - 根据设备能力选择最佳实现

删除的文件（已移动）：
- ~~LowPassFilter.cs~~ → Runner.Core.SignalProcessing
- ~~PeakDetector.cs~~ → Runner.Core.SignalProcessing
- ~~StepFrequencyCalculator.cs~~ → Runner.Core.SignalProcessing.FrequencyCalculator

## 项目依赖关系

```
Runner.Client
    ↓ (引用)
Runner.Business
    ↓ (引用)
Runner.Core
```

## 命名空间变更

| 原命名空间 | 新命名空间 |
|-----------|-----------|
| Runner.Client.Platforms.Android.Native.LowPassFilter | Runner.Core.SignalProcessing.LowPassFilter |
| Runner.Client.Platforms.Android.Native.PeakDetector | Runner.Core.SignalProcessing.PeakDetector |
| Runner.Client.Platforms.Android.Native.StepFrequencyCalculator | Runner.Core.SignalProcessing.FrequencyCalculator |
| Runner.Client.Platforms.Android.Native.IStepCounter | Runner.Business.StepCounter.IStepCounter（业务接口）<br>Runner.Client.Platforms.Android.Native.IStepCounter（平台接口） |

## API 变更

### FrequencyCalculator（原 StepFrequencyCalculator）
- `AddStep(long timestampMs)` → `AddEvent(long timestampMs)` - 更通用的命名
- `GetCadence(long currentTimeMs)` → `GetFrequency(long currentTimeMs)` - 更通用的命名

## 优势

1. **可复用性**：信号处理算法可在其他项目中使用（心率检测、震动检测等）
2. **清晰分层**：Core（算法）→ Business（业务）→ Client（UI/平台）
3. **平台无关**：Core 和 Business 层完全不依赖 Android
4. **易于测试**：纯算法类可以独立单元测试
5. **职责清晰**：每层只关注自己的职责

## 后续可优化

1. 为 Core.SignalProcessing 编写单元测试
2. 考虑将 FrequencyCalculator 的窗口大小参数化（目前硬编码 8 秒）
3. 考虑为 IStepCounter 添加更多业务方法（如 Reset、GetCurrentSteps 等）

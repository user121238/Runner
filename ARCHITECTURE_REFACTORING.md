# 架构重构总结 - 分层架构实现

## 重构目标
将所有业务逻辑和平台实现从 Client 层移到 Business 层，Client 层只保留 UI 相关代码（Views、ViewModels、App、MauiProgram）。

## 最终架构

```
Runner.Client (UI 层)
    ├── Views/              # XAML 视图
    ├── ViewModels/         # 视图模型
    ├── App.xaml.cs         # 应用入口
    ├── AppShell.xaml.cs    # Shell 导航
    ├── MauiProgram.cs      # 依赖注入配置
    └── Platforms/Android/  # 平台入口（MainActivity, MainApplication）

Runner.Business (业务逻辑层)
    ├── Models/             # 业务模型
    └── Platforms/Android/  # Android 平台实现
        ├── StepCounter/    # 计步器实现
        ├── Media/          # 媒体控制和音频播放
        ├── Permissions/    # 权限定义
        └── Services/       # Android 服务

Runner.Core (核心算法层)
    └── SignalProcessing/   # 信号处理算法
        ├── LowPassFilter.cs
        ├── PeakDetector.cs
        └── FrequencyCalculator.cs
```

## 文件移动详情

### 从 Runner.Client 移到 Runner.Business

#### Models（业务模型）
- `Models/User.cs` → `Runner.Business.Models.User`
- `Models/Product.cs` → `Runner.Business.Models.Product`

#### StepCounter（计步器实现）
- `Platforms/Android/Native/IStepCounter.cs` → `Runner.Business.Platforms.Android.StepCounter.IStepCounter`
- `Platforms/Android/Native/AccelerometerStepCounter.cs` → `Runner.Business.Platforms.Android.StepCounter.AccelerometerStepCounter`
- `Platforms/Android/Native/HardwareStepCounter.cs` → `Runner.Business.Platforms.Android.StepCounter.HardwareStepCounter`
- `Platforms/Android/Native/StepCounterFactory.cs` → `Runner.Business.Platforms.Android.StepCounter.StepCounterFactory`

#### Media（媒体控制）
- `Platforms/Android/Native/MediaController.cs` → `Runner.Business.Platforms.Android.Media.MediaController`
- `Platforms/Android/Native/CadencePlay.cs` → `Runner.Business.Platforms.Android.Media.CadencePlay`

#### Permissions（权限）
- `Platforms/Android/Native/ActivityRecognitionPermission.cs` → `Runner.Business.Platforms.Android.Permissions.ActivityRecognitionPermission`

#### Services（服务）
- `Platforms/Android/Services/RunnerService.cs` → `Runner.Business.Platforms.Android.Services.RunnerService`

### Client 层保留的文件（仅 UI）

```
Runner.Client/
├── App.xaml.cs                    # 应用入口
├── AppShell.xaml.cs               # Shell 导航
├── MauiProgram.cs                 # 依赖注入配置
├── Platforms/Android/
│   ├── MainActivity.cs            # Android 主活动
│   └── MainApplication.cs         # Android 应用类
├── ViewModels/
│   └── MainViewModel.cs           # 主页视图模型
└── Views/
    └── MainPage.xaml.cs           # 主页视图
```

## 命名空间变更

| 原命名空间 | 新命名空间 |
|-----------|-----------|
| Runner.Client.Models | Runner.Business.Models |
| Runner.Client.Platforms.Android.Native | Runner.Business.Platforms.Android.StepCounter |
| Runner.Client.Platforms.Android.Native | Runner.Business.Platforms.Android.Media |
| Runner.Client.Platforms.Android.Native | Runner.Business.Platforms.Android.Permissions |
| Runner.Client.Platforms.Android.Services | Runner.Business.Platforms.Android.Services |

## API 变更

### MediaController
由于移除了对 `Platform.CurrentActivity` 的静态依赖，所有方法现在需要传入 Context：

```csharp
// 旧 API
MediaController.NextTrack();

// 新 API
var context = Platform.CurrentActivity!;
MediaController.NextTrack(context);
```

### StepCounterFactory
工厂方法现在需要传入 Context：

```csharp
// 旧 API
_stepCounter = StepCounterFactory.Create();

// 新 API
var context = Platform.CurrentActivity!;
_stepCounter = StepCounterFactory.Create(context);
```

### CadencePlay
初始化方法现在需要传入音频资源 ID，提高了灵活性：

```csharp
// 旧 API
_cadencePlay.Init();

// 新 API
_cadencePlay.Init(ResourceConstant.Raw.beep3);
```

## 项目依赖关系

```
Runner.Client
    ↓ (引用)
Runner.Business
    ↓ (引用)
Runner.Core
```

## 优势

1. **清晰的职责分离**
   - Client：只负责 UI 展示和用户交互
   - Business：包含所有业务逻辑和平台实现
   - Core：纯算法，完全平台无关

2. **更好的可测试性**
   - Business 层可以独立测试，不需要 UI 环境
   - Core 层可以编写纯单元测试

3. **更好的可复用性**
   - Business 层的实现可以在其他 MAUI 项目中复用
   - Core 层的算法可以在任何 .NET 项目中使用

4. **更好的可维护性**
   - UI 变更不会影响业务逻辑
   - 业务逻辑变更不会影响 UI
   - 算法优化不会影响业务逻辑

5. **符合 SOLID 原则**
   - 单一职责原则：每层只关注自己的职责
   - 依赖倒置原则：高层模块不依赖低层模块，都依赖抽象

## 重构过程中的调整

1. **接口简化**
   - 移除了 `Runner.Business.StepCounter.IStepCounter` 业务接口
   - 直接在 `Runner.Business.Platforms.Android.StepCounter.IStepCounter` 中定义所有方法
   - 原因：当前只有 Android 平台实现，过早抽象会增加复杂度

2. **资源注入改进**
   - `CadencePlay.Init()` 改为接收音频资源 ID 参数
   - 避免硬编码资源引用，提高灵活性

3. **依赖注入优化**
   - MediaController 和 StepCounterFactory 改为接收 Context 参数
   - 移除静态依赖，提高可测试性

## 后续优化建议

1. **依赖注入改进**
   - 将 StepCounterFactory 改为接口注入
   - 将 MediaController 改为实例服务而非静态类
   - 将 CadencePlay 注册为单例服务

2. **接口抽象**（当需要支持多平台时）
   - 恢复 `Runner.Business.StepCounter.IStepCounter` 业务接口
   - 为 CadencePlay 定义 `IAudioPlayer` 接口
   - 为 MediaController 定义 `IMediaController` 接口

3. **单元测试**
   - 为 Business 层编写单元测试
   - 为 Core 层编写单元测试
   - 使用 Mock 框架测试平台相关代码

4. **文档完善**
   - 为每个公共 API 添加 XML 文档注释
   - 创建架构决策记录（ADR）
   - 编写开发者指南

## 文件统计

### Runner.Client（7 个文件）
- UI 相关：7 个文件
- 业务逻辑：0 个文件

### Runner.Business（12 个文件）
- 模型：2 个文件
- 计步器：4 个文件
- 媒体：2 个文件
- 权限：1 个文件
- 服务：1 个文件
- 其他：2 个文件

### Runner.Core（3 个文件）
- 信号处理：3 个文件

## 总结

通过这次重构，我们成功实现了清晰的三层架构：

1. **Core 层**：纯算法，零依赖，可在任何项目中复用
2. **Business 层**：业务逻辑和平台实现，可在其他 MAUI 项目中复用
3. **Client 层**：纯 UI，只负责展示和用户交互

这种架构使得代码更易维护、测试和扩展，符合软件工程的最佳实践。

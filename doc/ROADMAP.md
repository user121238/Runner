# Runner 路线图

更新：2026-10-09。对照 `doc/Runner.md`（需求规格 v1.3）盘点现状，并排定后续阶段。手表蓝牙线冻结，范围见文末。

## 现状结论

已完成并经真机验证的是算法与传感器接入：步频（硬件 / 加速度两档）、GPS 距离与速度、单调时钟、168 个回归用例（Core 行覆盖 100%）。产品层——运动会话、跑步界面、设置、提醒、记录、轨迹——尚未开始。按需求条目约完成四分之一，按工程难度已过半。

## 完成度盘点

| 需求 | 状态 | 说明 |
|---|---|---|
| 2.1 硬件计步 → 加速度兜底 | 完成 | `HardwareStepCounter`、`AccelerometerStepCounter`，三轮真机验证，见 `ALGORITHM_VALIDATION.md` |
| 2.1 无传感器时仅节拍器 | 未做 | `StepCounterFactory` 直接抛异常，节拍器不会接管 |
| GPS 距离 / 速度 | 完成 | 室外已知路线、长时间锁屏、加速度兜底精度尚未实测 |
| 节拍器 | 骨架 | `CadencePlay` 可用，目标步频写死 180，无设置 |
| 媒体控制 | 完成 | `MediaController` 发媒体键 |
| 前台服务 | 骨架 | 授权后即启动，未与跑步会话绑定；`RunnerService.StartBeeping` 为空循环死代码 |
| 2.2 阈值 / 语音 / 变调 / 震动 / 开关 | 未做 | 无任何实现 |
| 2.3 地图 | 未做 | 无任何实现 |
| 3 极简跑步主界面 | 未做 | `MainPage` 为调试面板；底栏四个按钮未绑命令；`AppShell` 空壳 |
| 4 LiteDB Workouts / Activities / Settings | 未做 | 仅注册了 `AddLiteDbDataAccess`，无实体；`Product`、`User` 为框架示例遗留 |
| 时长 | 未做 | 无计时 |

## 设计决定

- 核心数据结构是运动会话 `RunSession`：状态机 Idle / Running / Paused / Stopped，时长、距离、实时步频、目标步频、轨迹点、步频采样。Activity 记录是会话落盘，Workout 是会话的阶段计划，提醒是会话内实际步频对目标步频的判定。
- 一个 Start / Pause / Stop 驱动 GPS、计步、节拍器、前台服务；前台服务随会话起停。
- 可测逻辑放 `Runner.Core`（会话状态机、提醒判定、阶段推进），Android 绑定放 `Runner.Business`，与现有分层一致，直接进现有 MSTest 项目。
- 持久化复用 `DM.Maui.Framework.Data` 的 `IRepository<T>`、`IUnitOfWork`、`BaseEntity`、`LiteDbRepository<T>`，不自写 LiteDB 层。
- 地图 v1 用 `GraphicsView` 画轨迹折线：离线、零依赖；保留 `IMapProvider` 作为将来接 SDK 的切换点。真地图目前没有免费可用渠道，不接。
- 现有调试页保留为设置中的"诊断"入口，不删。
- 一个文件一个类型：接口、实现、枚举、实体各自独立文件。

## 阶段

### P0 解除构建阻塞 — 完成（2026-10-09）

`Runner.Lib` 纳入解决方案后触发 NU1008：`Runner.Lib.csproj` 去掉内联 `Version`，`Directory.Packages.props` 集中声明 `LiteDB`。

### P1 运动会话 — 未开始

- `Runner.Core.Session.RunSession`：状态机、时长（注入 `TimeProvider`）、距离、步频、目标步频、`TrackPoints`（复用 `GeoCoordinate`）、`CadencePoints`。
- Business 控制器把 `ILocationTracker`、`IStepCounter`、`CadencePlay`、`RunnerService` 绑到会话；一个 Start / Pause / Stop。
- 工厂无传感器时返回"无计步"而非抛异常，会话进入仅节拍器模式，隐藏实际步频。
- 验收：锁屏 10 分钟时长 / 距离 / 步数连续；暂停期间三者不累计；状态机全部迁移有 Core 测试。

### P2 极简主界面 — 未开始

- 新主页：时长、距离、实际步频、目标步频四个大数字，一个大按钮，深色高对比。
- `AppShell` 三个 Tab：首页、记录、设置（计划页在 P4 加）。
- 调试页挂到设置中的"诊断"；主页去掉"连接手表跑步"入口。

### P3 设置与提醒 — 未开始

- `Settings` 实体：目标步频、允许偏差、音频 / 震动 / 语音开关、音效；走 `IRepository<T>`。
- `Runner.Core` 提醒判定：偏差持续 N 秒触发、冷却 M 秒、带回滞，纯逻辑可测。
- Android 输出：`CadencePlay` 扩展多音（beep1 / beep2 区分快慢），`Vibrator` 短振 / 长振，`TextToSpeech` 最后做。
- 节拍器目标步频来自设置。

### P4 记录与计划 — 未开始

- `Activity : BaseEntity`（StartTime、EndTime、TotalDistance、AverageCadence、TrackPoints、CadencePoints），Stop 时落盘；删除 `Product`、`User`。
- `Workout : BaseEntity`（阶段：时长、目标步频、AutoNextTrack）；阶段推进逻辑放 Core，`AutoNextTrack` 调 `MediaController.NextTrack`。
- 记录页：列表 + 详情；计划页：编辑与执行。

### P5 轨迹图 — 未开始

- `IMapProvider`：`StartTracking`、`DrawPath`、`UpdateUserLocation`。
- `GraphicsView` 实现：按经纬度等距投影画折线与当前位置，用于跑步中与记录详情。

### P6 真机验收与清理 — 未开始

- 室外已知长度路线多次往返、锁屏长跑、加速度兜底精度。
- 删除 `RunnerService.StartBeeping`；处理过期文档（见下）。

## 冻结范围：手表蓝牙

以下代码保留并继续编译，不投入：`Services/Ble/*`（`WatchBleService` 系列、`WatchRfcommService`、`WatchAuthService`、`DeviceEncryptInterop`、`IWatchBleService`）、`Models/WatchBle/*`、`Services/Database/MiHealthDataReader`、`WatchWorkoutPage` / `WatchWorkoutViewModel`、`BlePermissionHelper`、`Platforms/Android/java` 与 `libs` 原生库、Manifest 中的 `BLUETOOTH_*` 权限、`MauiProgram` 中的 BLE 注册。

## 过期文档

`GPS_DISTANCE_TRACKING.md`、`GPS_FILTER_DEBUG_GUIDE.md`、`MAINPAGE_GPS_EXAMPLE.md` 记录的 5m / 15km/h / 0.5s 为旧参数，当前默认值见 `ALGORITHM_VALIDATION.md`；`DEMO_README.md` 为框架示例说明；根目录 `Runner.md` 与 `doc/Runner.md` 内容相同。P6 统一处理。

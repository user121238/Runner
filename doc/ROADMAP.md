# Runner 路线图

更新：2026-10-10。对照 `doc/Runner.md`（需求规格 v1.3）盘点现状，并排定后续阶段。手表蓝牙线冻结，范围见文末。

## 现状结论

已完成并经真机验证的是算法与传感器接入：步频（硬件 / 加速度两档）、GPS 距离与速度、单调时钟、168 个回归用例（Core 行覆盖 100%）。产品层——运动会话、跑步界面、设置、提醒、记录、轨迹——尚未开始。按需求条目约完成四分之一，按工程难度已过半。

## 完成度盘点

| 需求 | 状态 | 说明 |
|---|---|---|
| 2.1 硬件计步 → 加速度兜底 | 完成 | `HardwareStepCounter`、`AccelerometerStepCounter`，三轮真机验证，见 `ALGORITHM_VALIDATION.md` |
| 2.1 无传感器时仅节拍器 | 完成（P1） | `StepCounterFactory.Create` 返回 null，会话进入仅节拍器模式 |
| GPS 距离 / 速度 | 完成 | 室外已知路线、长时间锁屏、加速度兜底精度尚未实测 |
| 节拍器 | 完成（P4 课程） | 目标步频来自课程阶段，0 = 不播；自由跑不播 |
| 媒体控制 | 完成 | `MediaController` 发媒体键 |
| 前台服务 | 完成（P1） | 随运动会话起停 |
| 2.2 阈值 / 语音 / 变调 / 震动 / 开关 | 不做（用户决定） | 步频偏离提醒已撤回；提醒改为课程节点切歌 |
| 2.3 地图 | 轨迹草图（P5） | 记录详情用 `GraphicsView` 画轨迹；真地图不接 |
| 3 极简跑步主界面 | 完成（P2） | `RunView` 四个大数字 + 主按钮；底栏在 Prism 区域内切换首页 / 记录 / 设置；诊断页独立 |
| 4 LiteDB Workouts / Activities / Settings | 完成（Settings 不再需要） | `WorkoutPlan`、`ActivityRecord` 走 DM `IRepository<T>` |
| 时长 | 完成（P1） | `RunSession.Elapsed` 只累计跑动区间 |

## 设计决定

- 核心数据结构是运动会话 `RunSession`：状态机 Idle / Running / Paused / Stopped，时长、距离、实时步频、目标步频、轨迹点、步频采样。Activity 记录是会话落盘，课程（`WorkoutPlan`）是会话上的阶段计划，推进器只读会话的时长与距离。
- 一个 Start / Pause / Stop 驱动 GPS、计步、节拍器、前台服务；前台服务随会话起停。
- 可测逻辑放 `Runner.Core`（会话状态机、课程推进），Android 绑定放 `Runner.Business`，与现有分层一致，直接进现有 MSTest 项目。
- Client 导航用 Prism.Maui（9.0.537 + Prism.DryIoc.Maui）：根页以 `RegionManager` 区域承载 `ContentView` 切换，需要独立一屏的用 `INavigationService` 推 `ContentPage`，根为 `NavigationPage/MainPage`。VM 继承 `BindableBase`，命令用 Prism 的 DelegateCommand / AsyncDelegateCommand，不引入 CommunityToolkit.Mvvm。
- 持久化走 DM 的 `IRepository<T>`：DM 1.0.4 起 `AddLiteDbDataAccess` 注册开放泛型 `IRepository<>` 单例，应用级单例可直接注入；框架随需求演进（源码 `D:\Eric\Code\DM.Maui.Framework`，Release 构建自动打包到 LocalNuGet）。
- 持久化复用 `DM.Maui.Framework.Data` 的 `IRepository<T>`、`IUnitOfWork`、`BaseEntity`、`LiteDbRepository<T>`，不自写 LiteDB 层。
- 地图 v1 用 `GraphicsView` 画轨迹折线：离线、零依赖；保留 `IMapProvider` 作为将来接 SDK 的切换点。真地图目前没有免费可用渠道，不接。
- 现有调试页保留为设置中的"诊断"入口，不删。
- 一个文件一个类型：接口、实现、枚举、实体各自独立文件。
- 权限是动作的前提，不是页面的前提：首次点击「开始」时申请权限并创建 GPS / 计步来源（诊断页进入时同样初始化），页面显示不依赖权限，也不挂在任何导航回调上。

## 阶段

### P0 解除构建阻塞 — 完成（2026-10-09）

`Runner.Lib` 纳入解决方案后触发 NU1008：`Runner.Lib.csproj` 去掉内联 `Version`，`Directory.Packages.props` 集中声明 `LiteDB`。

### P1 运动会话 — 代码完成（2026-10-09），待真机验收

- `Runner.Core.Session`：`RunSession` 状态机（Idle / Running / Paused / Stopped，非法转换抛异常）、时长只累计 Running、距离 / 步数 / 步频 / 轨迹只在 Running 接收，`CadenceSample` 采样；平均步频 = 步数 / 跑动分钟。20 个用例。
- `RunSessionController`（Business / Android）：一个 Start / Pause / Stop 驱动 GPS、计步、节拍器、前台服务；前台服务随会话起停；单个来源启动失败通过 `Error` 上报，会话继续。
- `StepCounterFactory.Create` 无传感器时返回 null，会话进入仅节拍器模式（`CadenceAvailable = false`，界面隐藏实际步频）。
- 主页新增"运动会话"面板作为 P2 之前的临时入口，原诊断面板保留。
- 待真机验收：锁屏 10 分钟时长 / 距离 / 步数连续；暂停期间三者不累计；GPS 未开启时会话只剩节拍与计步并提示。

### P2 极简主界面 — 代码完成（2026-10-09），待真机截图验收

- 导航骨架换成 Prism.Maui：根页 `MainPage` 以 `MainRegion` 承载首页 `RunView` / 记录 `HistoryView` / 设置 `SettingsView` 三个 `ContentView`，底栏 `RequestNavigate` 切换；诊断页 `DiagnosticsPage` 为独立 `ContentPage`，从设置页推入。`AppShell` 删除。
- 首页：时长 / 距离 / 实际步频 / 目标步频四个大数字 + 一个主按钮（开始 / 暂停 / 继续）+ 进行中才出现的结束键，黑底白字；无计步来源时步频显示"—"。
- `RunSessionHost`（Business）成为应用级单例：权限申请、GPS / 计步 / 节拍器来源与会话控制器的唯一所有者，首页与诊断页都只消费它，不各自建监听器。权限与来源在首次点击「开始」时申请、创建（2026-10-10 改，见 P6），首页显示不依赖它们。
- 记录页为占位（P4 填充）；设置页只有"诊断"入口（P3 填充）；"连接手表跑步"入口移除。
- DM.Maui.Framework.Core 的 Shell / 导航只为冻结的手表页面保留编译；DM.Data 继续用。
- 真机：Debug 包已安装并启动无崩溃（包名实际为 Manifest 的 `com.eric.runner`，非 csproj 的 ApplicationId）；当时手机锁屏且 MIUI 拒绝 adb 注入按键，界面截图待解锁后补。

### P3 设置与提醒 — 已撤回（2026-10-09）

用户决定不做步频偏离提醒，也不在设置里设目标步频：`Runner.Core.Alerts`、提示音 / 变调 / 震动输出、`RunSettings` 与设置页里的相关项全部删除（实现见提交 afd9ada，随后回退）。步频改为由课程阶段定义，提醒改为课程节点切歌。

### P4 课程与记录

#### 课程 — 代码完成（2026-10-09），待真机验收

- `Runner.Core.Workouts`：`WorkoutPlan`（名称、轮数、阶段列表）、`WorkoutStage`（步频 0–300，0 = 不播节拍；结束条件 时长秒 / 距离米 二选一；进入时是否切歌）、`WorkoutRunner` 用会话的跑动时长与距离推进阶段——边界按计划值累加不带入采样延迟，一次更新可跨多阶段，轮次展开。11 个用例。
- 持久化：`WorkoutStore` 走 DM 的 `IRepository<WorkoutPlan>`；DM 1.0.4 在 `AddLiteDbDataAccess` 里补了开放泛型 `IRepository<>` 单例注册（`LiteDbRepository<T>` 只持有共享模式的单例 `ILiteDatabase`）。
- 会话：`RunSessionController.StartAsync(WorkoutPlan?)`，进入阶段时按步频起停节拍器、按需 `MediaController.NextTrack` 切歌；课程完成停节拍，会话继续；自由跑不播节拍。
- 界面：底栏加「课程」；课程列表（新建 / 编辑 / 删除，删除前确认）与编辑页（名称、轮数、阶段：步频、时长分钟 / 距离米、切歌开关）；首页空闲时选课程或自由跑，进行中显示「课程 · 第 r/R 轮 · 阶段 i/n · 剩 x」。
- 真机验收：法特莱克（2 分钟 180 / 1 分钟 120 切歌 × N 轮）节点准时切节拍与切歌；暂停不计入阶段时长；距离阶段按 GPS 距离推进。

#### 记录 — 代码完成（2026-10-09），待真机验收

- `Runner.Core.Activities`：`ActivityRecord`（起止时间、时长、距离、步数、平均步频、均速、课程名、轨迹点、步频采样）由已结束的会话生成；短于 5 秒的会话不落盘。3 个用例。
- `ActivityStore` 走 DM `IRepository<ActivityRecord>`；`RunSessionController.Stopped` 事件 → `RunSessionHost` 落盘并提示"记录已保存"。
- 记录页：按时间倒序列表（日期、公里、时长、均步频、课程名），点开详情，删除前确认；详情页展示指标与轨迹草图。
- `Product`、`User` 框架示例遗留已删除。

### P5 轨迹图 — 记录详情完成（2026-10-09）

- `TrajectoryDrawable`（`GraphicsView`）：等距投影（经度按中心纬度余弦缩放）后按比例铺满画布画折线，起点白、终点红，无轨迹时提示。
- 只用于记录详情；跑步中主界面保持四个数字，不加图。
- 没有引入 `IMapProvider` 抽象：只有一个实现时接口是空壳，真要接 SDK 时再抽。

### P6 真机验收与清理 — 清理完成（2026-10-09），真机验收进行中（2026-10-10）

- 已清理：删除 `RunnerService.StartBeeping` 死代码、`Product` / `User` 示例模型、过期文档（见下）；csproj `ApplicationId` 对齐为 Manifest 的 `com.eric.runner`。
- 真机第一轮（2026-10-10）：最新包安装、启动无崩溃，首页 / 课程页渲染正常，Prism 区域切换正常。发现并修复：首页初始化挂在区域导航回调上，而 `RegisterViewWithRegion` 填充的初始视图不触发 `IRegionAware.OnNavigatedTo`，权限与来源从未初始化、状态卡在"正在申请权限…"。改为首次点击「开始」时申请权限并创建来源（诊断页进入时同样初始化），首页只在构造时读课程列表。
- 待真机验收：课程新建 / 编辑；法特莱克课程节点切节拍与切歌；暂停 / 继续 / 结束；记录落盘、列表、详情与轨迹草图；诊断页；室外已知长度路线多次往返、锁屏长跑、加速度兜底精度。

### P7 UI 人性化 — 代码完成（2026-10-10），待真机截图验收

- 设计令牌集中在 `Resources/Styles/Colors.xaml`（近黑底、荧光绿主色、暂停琥珀、结束红）与 `Styles.xaml`（七档字号、卡片 / 芯片 / 胶囊 / 幽灵按钮 / 输入框样式）；页面只引用令牌，不再散写颜色与字号；强制深色主题。
- 首页三态：空闲 = 课程芯片 + 摘要卡 + 大「开始」；跑动 = 分:秒计时器 + 距离 / 步频 / 目标步频三列 + 课程阶段进度条、剩余量与下一阶段预告；结束 = 成绩 + 再来一次。暂停琥珀、结束红，结束前确认。
- 底栏换成线性图标 + 文字、选中态荧光绿；课程 / 记录卡片化（点卡片进编辑 / 详情，删除退为幽灵按钮），空态有图标与指引；编辑页分段式「按时长 / 按距离」、输入框去 Android 下划线、键盘弹出底部避让；详情页距离为主数字、六格统计卡。
- 诊断页按调试工具对待，不重做。
- Logo 与开屏（2026-10-10）：单线 R 标记（荧光绿 / 近黑，右腿拉成前冲弧线），`Resources/AppIcon`（自适应图标，标记最远点在半径 57%，安全区 61% 内）、`Resources/Splash`（放大 1.5 倍，Android 12+ 圆形开屏槽内不裁）、`Platforms/Android/Resources/drawable/ic_stat_runner.xml`（前台通知纯白单色版，服务通过 `RunnerService.SmallIconResourceId` 取用）；应用名 Runner；原生 `colors.xml` 与 csproj 图标 / 开屏底色对齐令牌，模板紫清零。

### P8 配速 — 代码完成（2026-10-10），待真机验收

- `RunSession` 记录（跑动时长, 累计距离）序列；`PaceCalculator`（Core）：实时配速 = 最近 30 秒窗，最快 / 最慢 = 100 米最紧滑窗取极值、停留超 10 秒断段、慢于 20'/km 不计，平均 = 时长 / 距离。15 个用例，规则见 `ALGORITHM_VALIDATION.md`。
- `ActivityRecord.DistanceSamples` 随记录落盘，详情页据此重算最快 / 最慢；旧记录显示 —。
- 界面：跑动中 2×2（距离 / 配速、步频 / 目标步频）；结束后 2×3（距离 / 平均配速 / 均步频、最快 / 最慢配速 / 步数）；记录列表与首页「上次跑步」卡用配速替代 km/h；详情页六格改为时长 / 平均 / 均步频、最快 / 最慢 / 步数，均速与轨迹点数退到页脚。

## 冻结范围：手表蓝牙

以下代码保留并继续编译，不投入：`Services/Ble/*`（`WatchBleService` 系列、`WatchRfcommService`、`WatchAuthService`、`DeviceEncryptInterop`、`IWatchBleService`）、`Models/WatchBle/*`、`Services/Database/MiHealthDataReader`、`WatchWorkoutPage` / `WatchWorkoutViewModel`、`BlePermissionHelper`、`Platforms/Android/java` 与 `libs` 原生库、Manifest 中的 `BLUETOOTH_*` 权限、`MauiProgram` 中的 BLE 注册。

## 过期文档

已于 P6 删除：`GPS_DISTANCE_TRACKING.md`、`GPS_FILTER_DEBUG_GUIDE.md`、`MAINPAGE_GPS_EXAMPLE.md`（记录的 5m / 15km/h / 0.5s 为旧参数，当前默认值见 `ALGORITHM_VALIDATION.md`）、`DEMO_README.md`（框架示例说明）、根目录 `Runner.md`（与 `doc/Runner.md` 重复）。`ARCHITECTURE_REFACTORING.md`、`REFACTORING_SUMMARY.md` 作为历史记录保留。

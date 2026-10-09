# Runner APP 需求规格说明书 (草案 v1.3)

## 1. 核心技术选型更新

* **框架**：.NET 10 (MAUI)
* **平台**：Android Only (API 30+)
* **UI 风格**：**极简主义 (Minimalist)**。强调高对比度数据展示，排除复杂的装饰性元素，确保跑步过程中信息一目了然。
* **地图策略**：首选 Google Maps，通过接口层解耦，支持未来平滑切换到高德/百度。

---

## 2. 核心功能进阶设计

### 2.1 智能步频检测系统 (Hybrid Cadence Detection)

系统将采取 **自动降级策略** 确保最大的设备兼容性：

1. **一级优先**：使用 Android 硬件计步传感器 (`Sensor.TYPE_STEP_COUNTER`)。优点：功耗极低，系统级过滤，数据准确。
2. **二级方案**：若无计步传感器，激活加速度计 (`Sensor.TYPE_ACCELEROMETER`)。
* **自研算法**：采用三轴合力计算 + 波峰检测 (Peak Detection) + 低通滤波，实时识别步行频率。


3. **最终方案**：若两者皆无，则关闭实际步频实时显示功能，仅进行节拍器播报。

### 2.2 用户自定义提醒系统 (Smart Notification)

* **阈值设置**：用户可设置步频允许的偏差范围（如  SPM）。
* **多模态提醒**：
* **音频反馈**：语音提示“步频过快/过慢”或改变节拍器音调。
* **触感反馈**：手机震动提醒（支持短振/长振）。
* **开关控制**：所有提醒逻辑均可在设置中单独开启或关闭。



### 2.3 “插件化”地图架构

为实现你提到的“快速接入国内 SDK”，我们将采用 **策略模式 (Strategy Pattern)**：

* **定义 `IMapProvider` 接口**：包含 `StartTracking()`、`DrawPath()`、`UpdateUserLocation()` 等通用方法。
* **当前实现**：`GoogleMapsProvider`。
* **后续扩展**：只需新增 `AMapProvider` (高德) 实现接口，修改一行注入代码即可完成切换，无需改动运动核心逻辑。

---

## 3. UI/UX 风格导向：极简设计

为了符合你要求的“简洁”：

* **数据大屏**：跑步主界面仅保留 3-4 个核心指标（时长、距离、实际步频、目标步频），使用加粗的大号无衬线字体。
* **高对比度**：采用黑白底色或深色模式，减少渐变和投影。
* **交互逻辑**：所有操作尽量通过大按钮实现，防止跑步中误触。

---

## 4. 数据库结构设计 (LiteDB)

考虑到离线存储和数据结构化，LiteDB 的集合建议如下：

* **Workouts (训练定义)**：存储课程设置，如 `[{Stage: 1, Duration: 300s, TargetSPM: 180, AutoNextTrack: true}]`。
* **Activities (运动记录)**：
* `StartTime` / `EndTime`
* `TotalDistance` / `AverageCadence`
* `TrackPoints` (List of Coordinates: Lat, Lng, Alt, Time)
* `CadencePoints` (List of Actual SPM vs Time)


* **Settings**：用户偏好、提醒开关、自定义音效路径。

---

# Runner

基于 **.NET 10 MAUI** 的离线跑步应用，把运动计时、课程节拍、GPS 轨迹、计步、配速和历史记录放在同一个会话里。

当前代码标签：[0.0.1-alpha.2](https://github.com/user121238/Runner/tree/0.0.1-alpha.2)，处于预发布阶段。

[版本与下载](https://github.com/user121238/Runner/releases) · [GitHub Actions](https://github.com/user121238/Runner/actions/workflows/ios-unsigned.yml) · [iOS 发布指南](doc/IOS_RELEASE.md)

## 当前功能

| 功能 | 说明 |
| --- | --- |
| 运动会话 | 开始、暂停、继续、结束；时长只累计跑动区间，统一管理定位、计步和节拍器 |
| 训练课程 | 多阶段、重复轮次；每阶段设置目标步频，以及按时长或距离结束 |
| 节拍器 | 按课程目标步频播放；步频为 0 时静音，自由跑不播放节拍 |
| 计步与步频 | 接入手机传感器，显示累计步数和实时步频；来源随平台与设备能力变化 |
| 距离与配速 | GPS 过滤后累计距离；显示实时、平均、最快和最慢配速，样本不足时显示 `—` |
| 历史记录 | 本地保存课程名、时长、距离、步数、步频及采样数据；支持列表、详情和删除 |
| 轨迹草图 | 在记录详情中绘制离线轨迹折线，没有地图底图或导航 |
| 诊断 | 单独查看定位、计步和音频状态；运动进行中禁止诊断页重置或停止共用来源 |

课程完成后停止节拍，运动会话由用户手动结束。**跑动时间不足 5 秒的会话不保存为历史记录。**

手表连接、蓝牙协议和配套原生加密桥接已移除。当前范围也不包含在线地图 SDK、账户/云同步和步频偏离语音或震动提醒。

## 平台支持

| 平台 | 最低版本 | 原生能力 |
| --- | --- | --- |
| Android | Android 10 / API 29 | 优先使用硬件计步，缺少硬件来源时使用加速度兜底；GPS 与前台服务 |
| iOS | iOS 15.0 | CoreLocation 定位、CMPedometer 系统计步、AVAudioPlayer 循环节拍 |

- **音乐控制**：Android 可以向当前播放器发送系统媒体键，实际响应取决于播放器；当前 Runner 尚未接入 iOS 播放器控制，仍可用耳机或控制中心切歌，相关应用内入口隐藏。节拍器可与音乐混音。
- **权限**：首次开始运动或进入诊断页时按需申请，而不是打开首页时申请。相关权限未授予时，对应指标缺失或降级。
- **数据**：课程和跑步记录保存在应用数据目录的 `runner.db`。沿用原 `WorkoutPlan`、`ActivityRecord` 集合和整数 ID，兼容旧记录。
- **依赖**：使用公开 NuGet 源；不依赖开发者本机的私有包或手表原生库。
- **iOS 运行时**：Release 也启用 Mono 解释器，让 LiteDB 的动态表达式与泛型方法正常执行；不改变已有数据库格式。此配置可能增加托管执行开销，性能仍需实测。

## 开发与构建

以下命令从仓库根目录执行。

### Android

准备 .NET 10 SDK、MAUI Android workload，以及与 workload 匹配的 Android SDK / JDK。仅运行下方的 .NET 回归测试时，无需安装移动平台 workload。

PowerShell 示例：

```powershell
dotnet workload install maui-android
New-Item -ItemType Directory -Force artifacts/build | Out-Null

dotnet build src/Runner.Client/Runner.Client.csproj `
  --configuration Debug `
  --framework net10.0-android `
  -p:RunnerTargetFrameworks=net10.0-android `
  -p:EmbedAssembliesIntoApk=true `
  '-bl:artifacts/build/android-{}.binlog'
```

APK 输出到 `src/Runner.Client/bin/Debug/net10.0-android/`。该命令只构建，不安装到设备。

Windows 默认只启用 Android 目标；macOS 默认同时包含 Android/iOS。使用 `RunnerTargetFrameworks` 可只还原、构建指定的移动平台，`Runner.Core` 仍保持纯 `net10.0`。

### iOS 无签名 IPA

完整设备包在 macOS 上使用 Xcode 构建。发布脚本固定以下工具链，避免 SDK 与 Xcode 版本错配：

| 工具 | 版本 |
| --- | --- |
| GitHub runner | `macos-15` |
| Xcode / iPhoneOS SDK | `26.2` |
| .NET SDK / workload set | `10.0.103` |
| MAUI workload | `10.0.20` |

确认 `dotnet --version` 为 `10.0.103`，并选择 Xcode 26.2 后：

```bash
dotnet workload install maui-ios --version 10.0.103 --source https://api.nuget.org/v3/index.json
bash scripts/build-unsigned-ipa.sh 0.0.1-alpha 1 artifacts/ios
```

输出为 `ios-arm64` 设备包，包含真实的 `Payload/Runner.Client.app/`，不是模拟器包。脚本校验版本、架构、签名残留、执行权限和 ZIP 结构，并生成 SHA-256 校验文件。

**IPA 未签名，安装前需要使用自己的 Apple 证书、匹配的描述文件和 entitlements 重新签名。** 它不是直接用于 TestFlight 或 App Store 分发的签名包。

完整工具链安装、产物位置与排错见 [iOS 发布指南](doc/IOS_RELEASE.md)。

## 自动版本发布

自动发布以 **推送 Git 标签** 为入口，普通分支推送或单独修改项目版本字段不会触发 IPA 构建。

支持带或不带 `v` 的三段版本，以及 SemVer 预发布标识，例如：

- 稳定版：`v1.2.3`、`1.2.3`
- 预发布版：`0.0.1-alpha`、`0.0.1-alpha.1`

先提交代码改动，再推送分支和新标签。下一次预发布示例：

```bash
git push origin master
git tag -a 0.0.1-alpha.3 -m "Runner 0.0.1-alpha.3"
git push origin 0.0.1-alpha.3
```

保留已发布标签的指向；新代码使用新标签，不覆盖已有的 `0.0.1-alpha`。

工作流会依次校验版本、运行回归测试、构建并验证无签名 IPA，**全部成功后**创建对应 GitHub Release 并上传 IPA 与校验文件。带预发布标识的版本标记为 **Pre-release**。

以 `0.0.1-alpha` 为例：

| 项目 | 值 |
| --- | --- |
| Git 标签 / Release 标题 | `0.0.1-alpha` |
| 应用显示版本 | `0.0.1`，仅使用数字三段 |
| 应用构建号 | GitHub Actions 的 `run_number` |
| IPA 文件名 | `Runner-0.0.1-alpha-ios-arm64-unsigned.ipa` |

在 Actions 中手动运行 **iOS unsigned IPA** 时，填写 `0.0.1-alpha` 这类版本号，**只生成 Artifacts，不创建 Release**。IPA 构建产物保留 14 天，诊断日志保留 7 天。当前自动发布只打包 iOS，Android 使用本地构建流程。

**下载入口**：打开 Actions 运行总览中的 **IPA 下载** 摘要链接，或在页面底部的 **Artifacts** 下载 `ios-unsigned-*` ZIP 后解压。标签发布成功后，也可从 **Releases → 对应版本 → Assets** 直接下载 `.ipa`；`ios-diagnostics-*` 是日志，不是安装包。

## 测试

### 算法、存储与节拍音频

```powershell
dotnet test tests/Runner.Core.Tests/Runner.Core.Tests.csproj `
  --configuration Release `
  --logger trx `
  --results-directory artifacts/tests `
  '-bl:artifacts/tests/test-{}.binlog'
```

测试直接引用 Core，并链接无平台依赖的生产 Store 与 PCM 处理源码；覆盖会话状态、定位过滤、计步、课程推进、配速、旧数据库兼容和节拍音频生成。覆盖率与详细边界见 [测试说明](tests/README.md)。

### 发布脚本

Python 3.10+，仅使用标准库。Windows / PowerShell：

```powershell
$env:PYTHONUTF8 = "1"
python -m unittest discover -s scripts/tests -v
```

macOS / Linux：

```bash
PYTHONUTF8=1 python3 -m unittest discover -s scripts/tests -v
bash -n scripts/build-unsigned-ipa.sh
```

截至 `0.0.1-alpha.2`，本地已通过 **237 项 .NET 回归测试、38 项发布脚本与布局结构测试**，以及 Android Debug 完整构建、iOS Release 托管代码编译。托管编译与结构测试不等于 iPhone 真机验收通过；云端构建状态以 [Actions](https://github.com/user121238/Runner/actions/workflows/ios-unsigned.yml) 为准。

## 已知限制与验收范围

- GPS、步数和配速受定位质量、设备传感器及携带方式影响；合成信号测试不代表真实测量精度。
- 实时配速使用时间窗，最快/最慢配速使用距离窗；短距离、低质量或稀疏定位可能缺少配速结果。
- 后台运动受平台权限与系统策略约束。iOS 无定位且当前不播放节拍时，应用可能被挂起，阶段推进会延后。
- iPhone 授权流程、长时间锁屏、来电后的音频恢复，以及室外已知路线和加速度兜底精度，仍需真机验收。
- 遇到加载或保存异常时，可在提示区域点击“复制错误详情”保留完整堆栈；课程加载失败有重试入口，界面不会把失败当成空列表。

## 主要目录

```text
src/Runner.Core/          纯 .NET 算法、会话状态、课程和记录模型
src/Runner.Business/      共用会话控制、平台适配与 LiteDB 存储
src/Runner.Client/        MAUI 页面、Prism 导航、资源与依赖注册
tests/Runner.Core.Tests/  算法、存储兼容与 PCM 回归测试
scripts/                 IPA 打包、版本校验与脚本测试
.github/workflows/       标签驱动的 iOS 构建与 Release 发布
doc/                     路线图、算法验证与发布指南
```

业务依赖方向为 `Runner.Client → Runner.Business → Runner.Core`。包版本集中在 [Directory.Packages.props](Directory.Packages.props)，平台差异收敛在 `IRunPlatform`，两端共用会话与课程逻辑。

## 文档

- [路线图与验收清单](doc/ROADMAP.md)
- [算法验证记录](doc/ALGORITHM_VALIDATION.md)
- [iOS 无签名 IPA 与 Release](doc/IOS_RELEASE.md)
- [回归测试说明](tests/README.md)
- [历史需求草案](doc/Runner.md)：保留讨论背景，不作为当前功能清单

# 算法回归测试

`Runner.Core.Tests` 直接通过 `ProjectReference` 测试真实的 `Runner.Core`，不复制算法，不依赖 Android、MAUI、设备、墙钟或等待。需要 .NET 10 SDK；首次运行需要还原 NuGet 测试包。

## 运行

以下为当前工作区的 PowerShell 命令。MSTest 使用 VSTest runner，覆盖率由 coverlet.collector 生成。

```powershell
rtk proxy dotnet test 'D:\Eric\Code\Runner\tests\Runner.Core.Tests\Runner.Core.Tests.csproj' `
  --configuration Release `
  --settings 'D:\Eric\Code\Runner\tests\Runner.Core.Tests\coverage.runsettings' `
  --collect 'XPlat Code Coverage' `
  --results-directory 'D:\Eric\Code\Runner\artifacts\tests' `
  --logger trx `
  '-bl:D:\Eric\Code\Runner\artifacts\tests\test-{}.binlog'
```

TRX、Cobertura 和唯一名称的 MSBuild binlog 全部写入已忽略的 `artifacts` 目录。仅测试项目及 Core 会构建，不需要安装移动平台 workload。

## 验证范围

- 计步累计：逐步事件、硬件累计基线、硬件重启、重置后下一步为 1、暂停期间步数排除。
- 步频：90/120/180/210/240 SPM，真实批量增量，8 秒窗口和 3 秒停止超时，重复/倒序时间戳，重置，以及定时读取与硬件回调交错时的零计数基线保留。
- 加速度兜底：15 组步频 × 采样率组合（25/50/100 Hz），静止噪声、方向变化、不规则采样、断流恢复、无效输入。
- GPS：Haversine 基准、单位转换、坐标和精度合法性、误差圈内静止抖动、正常跑速与异常跳点、过期/未来点、速度超时、暂停/断流分段、段内静止平均速度、快照与重置。
- 时间基准：开机纳秒时钟与固定 UTC 锚点的映射、迟到样本、100 ns 精度、`GetElapsedTime` 单位，以及 GPS UTC 与手机墙钟不一致时仍正确接受真实的当前定位；真正未来的单调时间样本继续拒绝。
- 运动会话：状态机合法 / 非法转换、时长只累计跑动区间、暂停与结束清零实时步频、非 Running 拒收距离 / 步数 / 步频 / 轨迹、平均步频与均速、采样与轨迹快照、无效输入。

## 合成信号的边界

加速度测试使用 2 秒静止预热、20 秒 `9.81 ± 3 m/s²` 单周期信号和 1 秒静止收尾。计数允许最多 1 步首尾滤波暂态误差；静止噪声必须为 0 步。GPS 的静止案例使用每 2 秒正负 4 米交替、每点 5 米精度。

这些测试校验确定性数学行为、输入处理和状态机回归；**不代表真实口袋、手持、摆臂或低质量 GPS 场景的精度保证**。真实误差仍需带人工步数/已知路线的设备数据验证。

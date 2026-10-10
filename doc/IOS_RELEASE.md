# iOS 无签名 IPA 与 GitHub Release

## 触发与产物

推送版本标签会运行 **iOS unsigned IPA**：`v` 前缀可选，数字版本后可带 SemVer 预发布标识。支持 `v1.2.3`、`1.2.3`、`0.0.1-alpha`、`v0.0.1-alpha.1`。成功后创建原标签名称的 GitHub Release，预发布版本标记为 **Pre-release**，附上：

- `Runner-0.0.1-alpha-ios-arm64-unsigned.ipa`（稳定版示例：`Runner-1.2.3-ios-arm64-unsigned.ipa`）
- 同名 `.ipa.sha256` 校验文件

```bash
git tag 0.0.1-alpha
git push origin 0.0.1-alpha
```

工作流文件须先存在于标签指向的提交。`v*` 和数字开头的标签只是触发范围，不是宽松的版本规则：`v1.2`、`v01.2.3`、`0.0.1-alpha.01` 等会在安装 workload 前失败。每个数字版本段为 `0..9999`，不带前导零；预发布标识由字母、数字、连字符及点分隔的非空片段组成，纯数字片段不带前导零。不接受 `+build` 元数据。

Actions 中手动运行 `workflow_dispatch` 时填写不带 `v` 的版本，如 `1.2.3` 或 `0.0.1-alpha`，**只保存 Actions Artifacts，不创建或修改 Release**，即使选中了一个标签。IPA Artifacts 保留 14 天；构建诊断保留 7 天。

同一标签的任务串行执行。重跑使用同名 IPA 和 `gh release upload --clobber`，替换该 Release 的 IPA 与校验文件，不复制 Release、不删除其他附件。请保持发布标签指向不变；需要改代码时使用新版本标签。

### 在哪里下载 IPA

- 打开对应 Actions **运行总览 / Summary**，而不是某个 job 的日志页。构建成功后，摘要中的 **IPA 下载** 会直接链接到产物 ZIP；也可在总览底部的 **Artifacts** 下载 `ios-unsigned-<run_id>-<run_attempt>`，解压后取得 `.ipa`、校验文件和 `metadata.json`。
- `ios-diagnostics-*` 只有诊断日志，不是 IPA。当前尝试失败或取消时，摘要会明确说明没有上传 IPA。
- 标签触发且 `release` job 成功后，IPA 同时出现在仓库 **Releases → 对应版本 → Assets**；手动运行不创建 Release，即使运行时选择的是标签。
- 以当前产物列表为准：旧日志中的“uploaded successfully”只证明当时上传成功；产物被清理后，旧下载链接可能返回 404，需要重新完成一次构建。

上传与下载 Actions 均固定为 Node 24 版本。GitHub 的 macOS arm64 容量提示仅说明排队可能延长，不代表编译或上传失败；排队时无需反复取消重跑。

## 版本规则

| 项目 | 来源 |
| --- | --- |
| `ApplicationDisplayVersion` / `CFBundleShortVersionString` | 纯数字版本；`0.0.1-alpha` 和 `v0.0.1-alpha.1` 都对应 `0.0.1` |
| IPA 文件名版本 | 去掉可选的 `v`，保留完整预发布标识，避免预发布包与稳定包混淆 |
| GitHub Release 标签 / 标题 | 原 Git 标签，保留是否带 `v`；有预发布标识时传入 `--prerelease=true` |
| `ApplicationVersion` / `CFBundleVersion` | `github.run_number`；同一次工作流重跑时保持不变 |
| 运行重试编号 | 只进入 Actions Artifact 名称，不改变应用版本 |

构建号接受 `1..9999`，符合 Apple 单段 `CFBundleVersion` 的四位数格式。达到上限时任务提前失败，需要明确升级构建号策略，而不是静默截断或回绕。脚本通过 MSBuild 命令行属性传入版本，不修改项目文件中的默认开发版本。

## 固定工具链

已于 **2026-10-10** 核对官方镜像与 NuGet workload 内容：

| 项目 | 固定值 |
| --- | --- |
| GitHub runner | `macos-15` |
| Xcode | `26.2`，`/Applications/Xcode_26.2.app`，镜像列出的构建号 `17C52` |
| iPhoneOS SDK | `26.2` |
| .NET SDK | `10.0.103` |
| workload set | `10.0.103` |
| iOS workload | `26.2.10197` |
| MAUI workload | `10.0.20` |

版本常量集中在 `scripts/ios_release.py`。工作流显式选择 Xcode，不依赖镜像默认版本；.NET 安装到 `runner.temp` 下的独立目录，不受镜像预装的新 SDK 影响。SDK 与 Xcode/SDK 不符合预期时停止构建。

依据：

- [GitHub macOS 15 镜像清单](https://github.com/actions/runner-images/blob/main/images/macos/macos-15-Readme.md)：核对时列有 Xcode 26.2 和 iPhoneOS 26.2；镜像版本 `20260824.0482.1`。
- [固定 workload-set NuGet 包](https://api.nuget.org/v3-flatcontainer/microsoft.net.workloads.10.0.100/10.103.0/microsoft.net.workloads.10.0.100.10.103.0.nupkg)：`data/microsoft.net.workloads.workloadset.json` 将 iOS 固定为 `26.2.10197`、MAUI 固定为 `10.0.20`。
- [固定 iOS manifest NuGet 包](https://api.nuget.org/v3-flatcontainer/microsoft.net.sdk.ios.manifest-10.0.100/26.2.10197/microsoft.net.sdk.ios.manifest-10.0.100.26.2.10197.nupkg)：`data/WorkloadDependencies.json` 要求 Xcode `[26.2,)`，推荐 `26.2`。
- [.NET workload install 版本参数](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-install#--version-version)。
- [Apple CFBundleVersion 格式](https://developer.apple.com/documentation/bundleresources/information-property-list/cfbundleversion)。

GitHub 的 runner 标签会更新，整个系统镜像不是固定快照。将来移除 Xcode 26.2 时，此工作流会明确失败；升级时必须一起核对 runner、SDK、workload set 与 Xcode，而不是将任意一项改成 `latest`。

## 打包与验证

只发布 Client 的 `net10.0-ios` / `ios-arm64` / `Release`，通过 `-p:RunnerTargetFrameworks=net10.0-ios` 让项目引用也只解析 iOS。Core 保持纯 `net10.0`。

明确关闭 `EnableCodeSigning`、`CodesignRequireProvisioningProfile`、`ArchiveOnBuild` 和 `BuildIpa`。不导入证书、描述文件或签名 secrets，不调用 Xcode 的签名归档/导出流程。先生成真实 `.app`，再在临时副本上清理可能随原生依赖带入的签名与 profile，并逐个使用 `codesign` 确认 Mach-O 未签名。

Python 打包器还会检查：

1. `Info.plist` 中的应用标识、显示版本、构建号与 iPhoneOS 平台。
2. 主可执行文件存在，且为 64 位 arm64 Mach-O，不是模拟器文件或重命名 ZIP。
3. 包内没有签名目录、provisioning profile 或 Mach-O `LC_CODE_SIGNATURE` 数据。
4. 符号链接保持相对链接且不指向包外；Unix 可执行权限保留。
5. ZIP CRC 与 `Payload/Runner.Client.app/...` 结构完整，随后输出 SHA-256。

结构为：

```text
Runner-1.2.3-ios-arm64-unsigned.ipa
└── Payload/
    └── Runner.Client.app/
        ├── Info.plist
        ├── Runner.Client
        └── ...应用资源与原生依赖
```

**这是待重新签名的设备包。** 普通 iPhone 安装、TestFlight 或 App Store 分发仍需要使用自己的 Apple 证书、正确的描述文件与 entitlements 重新签名；生成无签名 IPA 不等于已完成安装或商店提交。

## 本地命令

macOS 安装上述 SDK/workload 并选择 Xcode 后，在仓库根目录运行：

```bash
sudo xcode-select --switch /Applications/Xcode_26.2.app/Contents/Developer
dotnet workload install maui-ios --version 10.0.103 --source https://api.nuget.org/v3/index.json
bash scripts/build-unsigned-ipa.sh 1.2.3 42 artifacts/ios
```

脚本每次使用新的构建目录，避免拾取旧 `.app`。构建输出保留在 `artifacts/ios/build.*/`，诊断在 `artifacts/ios/logs/`；目录由调用者自行清理。每次 publish 都保存独立 binlog 和文本日志，失败也会上传诊断。binlog 可含源码、路径与 MSBuild 属性，分享前应检查内容。

预发布包将完整版本传入脚本，例如 `bash scripts/build-unsigned-ipa.sh 0.0.1-alpha 42 artifacts/ios`。脚本只给 Apple 的显示版本属性传入 `0.0.1`，打包与文件名仍使用 `0.0.1-alpha`。CI 另通过 `RELEASE_TAG` 环境变量将原标签保存进 `metadata.json`；该变量若与打包版本不一致，会提前失败。

不需要 Xcode 的脚本测试：

```bash
python3 -m unittest discover -s scripts/tests -v
bash -n scripts/build-unsigned-ipa.sh
```

测试覆盖版本/输入校验、真实 Payload 结构、版本错配、错误架构、签名残留、校验和、重复打包与签名检查失败处理。Windows 上可执行这些结构测试；它们不替代 macOS 上实际的 MAUI 编译、Apple 签名工具检查或 iPhone 安装测试。首次云端工作流的成功状态以 GitHub Actions 的实际结果为准。

## 权限与排错

- build job 只有 `contents: read`；checkout 不保留凭据。
- 独立 release job 才有 `contents: write`，不检出或执行仓库构建脚本。
- GitHub tag/input 都通过环境变量传递，校验后再作为命令参数；不拼进 Shell 程序。
- 手动运行不进入 release job；不需要配置额外 secrets。
- 找不到 Xcode：核对 runner 镜像清单并成套更新版本。
- workload / restore 失败：先查看诊断 Artifact；依赖源应只有公开 NuGet，不应含本机私有 feed。
- `.app` / plist / 签名验证失败：检查 binlog 与保留的构建目录，不发布未通过验证的附件。

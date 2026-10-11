# MicBridge · 麦克风直连

**照常唤起 Mac 的语音输入，Windows 麦克风自动开始传输；语音应用结束收音后自动关麦。**

适合用 Windows + UU 远程控制 Mac，又希望继续使用 Mac 系统听写、语音输入或其他语音 App 的场景。

```text
Windows 麦克风 → UU 端口直连 / 可信 TCP 隧道 → Mac → BlackHole 2ch → Mac 语音 App
```

MicBridge 只传声音，不做语音识别，不粘贴文字，不需要 API Key。它是独立的开源工具，与网易 UU 没有隶属或合作关系。

## 下载

[**打开 Releases 下载最新版**](https://github.com/Carooot/micbridge/releases/latest)

- **Windows**：`MicBridge-Windows-x64.zip`，解压后打开 `MicBridge.exe`。
- **Mac**：`MicBridge-macOS-universal.zip`，解压后打开 `MicBridge.app`。

Windows 10/11 x64，.NET Framework 4.8；macOS 14.2+，Apple Silicon / Intel 通用程序。需要在 Mac 单独安装 [BlackHole 2ch](https://github.com/ExistentialAudio/BlackHole)。本项目不捆绑声卡驱动。

发布包没有商业代码签名或 Apple 公证。macOS 若阻止打开，请按系统提示在“隐私与安全性”中仅允许这个应用，或自行从源码构建；不要关闭系统的整体安全保护。

## 三步开始

### 1. Mac 打开接收器

打开 `MicBridge.app`。窗口应显示 **BlackHole 2ch 已就绪**。

在语音 App 中选择 **BlackHole 2ch** 作为麦克风。使用系统默认输入的 App，可在“系统设置 → 声音 → 输入”里选择它。声音输出继续使用原来的扬声器或耳机。

### 2. Windows 开启 UU 端口直连

选中要连接的 Mac，添加并开启映射：

| 项目 | 值 |
|---|---|
| 协议 | TCP |
| 目标地址 | `127.0.0.1` |
| Mac 目标端口 | `18768` |
| Windows 本地端口 | `18768` |

也可以用其他可信 TCP 隧道，将 Windows 的 `127.0.0.1:18768` 转发到 Mac 同一地址。无需连接码。

### 3. Windows 连接，照常使用 Mac 语音输入

打开 `MicBridge.exe`，点 **连接 Mac**。麦克风可在右上角“设置”中选择。

在 Mac 用自己习惯的快捷键或按钮唤起语音输入，例如豆包输入法。MicBridge 检测到该应用开始读取 BlackHole 后，才通知 Windows 开麦；应用停止读取后，自动关闭 Windows 麦克风并清空播放队列。**无需再按 F8，也不用给 MicBridge 配快捷键。**

- Mac 设置中选择“跟随哪个应用”；检测到豆包输入法时默认跟随豆包，也可选择其他应用或所有使用 BlackHole 的应用。
- 应用列表来自当前音频进程。如果没出现，先打开语音应用，再重新打开设置。
- 两端均有“暂停联动”。默认关闭窗口后仍在后台待命；Windows 可从任务栏托盘退出，Mac 可用菜单退出。
- 待命时只保持连接，不采集 Windows 麦克风。断线、锁屏或休眠会停止采集，恢复后手动重新连接。

联动依据是应用的实际收音状态。如果某个应用一直占用 BlackHole，即使界面没有显示识别中，MicBridge 也会保持传输；可在设置中只跟随需要的应用，或暂停联动。识别、标点和文本输入仍由 Mac 语音应用负责。

## 从旧版升级

**Windows 和 Mac 必须同时升级至 1.1.0。** 新版自动联动协议与 1.0 的按住说话协议不兼容。

退出两端旧程序后再打开新版，UU 映射仍用 **18768**，BlackHole 设置不用改。不要让两个 Mac 接收器争用同一个端口。新版 Mac 接收器最低要求 macOS 14.2。

## 常见问题

| 现象 | 检查方式 |
|---|---|
| 连接不上 | 确认 Mac 接收器运行、UU 映射已开启、两边端口都是 18768。 |
| 提示端口被占用 | 退出旧接收器或另一份 MicBridge，再重新打开。 |
| Windows 音量条不动 | 先唤起 Mac 语音输入；确认两端未暂停、Mac 跟随了正确的应用、该应用使用 BlackHole，并允许 Windows 桌面应用访问麦克风。 |
| Mac 音量条动，App 没反应 | 在该 App 中选 BlackHole 2ch，并按要求授予该 App 麦克风权限。 |
| 黑洞声卡不可用 | 安装 BlackHole 2ch，按驱动要求完成重启，再点“重新检查声卡”。 |
| 结束说话后仍传输 | 检查语音应用是否还在收音。MicBridge 跟随输入流，不根据静音猜测结束；可点“暂停联动”。 |
| 网络断开或积压 | 工具会关闭麦克风。恢复隧道后手动重新连接。 |

## 隐私与信任范围

- 不落盘保存音频，不调用识别云服务，不收集遥测，不读写剪贴板。
- Windows 每次启动均处于断开状态。连接后，只有 Mac 请求且未暂停时才打开麦克风；不读取键盘或要求辅助功能权限。
- 接收端只监听 **127.0.0.1**，不直接监听局域网或公网。
- 按设计**不做额外身份认证或应用层加密**，依赖你配置的 UU 或其他可信隧道。任何能访问该本地转发端口的程序都可以连接；不要另行公开映射到公网。
- 48 kHz、16 bit、单声道 PCM，讲话时约 96 KB/s。检测、网络和设备启动会带来少量延迟。停止检测含约 250 ms 防抖，避免应用切换音频流时频繁开关麦克风；不预先录音。

## 构建与测试

只使用系统框架和 Python 标准库，不需要第三方运行库或识别模型。

### macOS

安装 Xcode Command Line Tools 和 Python 3，然后：

```sh
python3 scripts/build.py mac
python3 tests/integration.py
```

输出：`build/mac/MicBridge.app`。构建脚本同时生成 arm64 / x86_64 二进制并合并，运行协议和缓冲测试。

已安装 BlackHole 时，可额外验证音频回读、停止后的静音和实际输入流驱动的联动：

```sh
python3 tests/integration.py --audio
python3 tests/automatic.py
```

此测试向 BlackHole 发送生成的测试音，并从 BlackHole 输入读取强度与频率，不使用物理麦克风，不保存录音。请在其他应用未使用 BlackHole 时运行。自动联动测试还确认接收器的输出不会触发开麦循环。

### Windows

安装 Python 3，使用 .NET Framework 4.8 自带的编译器：

```powershell
python scripts/build.py windows
```

输出：`build/windows/MicBridge.exe`。脚本也会运行队列、自动开关控制、设置保存、真实 TCP 通知、协议边界和工作线程退出测试，并导出窗口预览。

### 打包

```sh
python scripts/package.py mac
python scripts/package.py windows
```

ZIP 输出在 `dist/`。GitHub Actions 在提交和拉取请求时执行两端构建与测试；发布 Release 时，测试通过后自动附上两端安装包和校验文件。

## 实现与验证范围

Windows 使用 WinMM 录音和有上限的音频队列；Mac 通过 CoreAudio 读取进程的输入流状态，使用 Network.framework 通信、AudioQueue 输出到 BlackHole。协议说明见 [PROTOCOL.md](PROTOCOL.md)。

基础音频转发版本已由使用者在 Windows → UU → Mac 场景确认可用。1.1 版已实测豆包输入流的开始/停止变化，自动化验证覆盖协议分片、待命、自动开始/停止、断线、队列清空、BlackHole 回读及输出不误触发输入。新版仍需要不同 Windows 麦克风、语音应用和长期弱网的真实设备反馈。

## 开源协议

[MIT](LICENSE)。BlackHole 是单独安装的外部项目，遵循其自己的开源协议。反馈问题时请描述系统、麦克风、连接步骤和报错，避免上传私人录音或账号凭据。

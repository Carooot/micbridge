# MicBridge · 麦克风直连

**按住 Windows 的一个键，把麦克风声音送给远端 Mac；松开就停止。**

适合用 Windows + UU 远程控制 Mac，又希望继续使用 Mac 系统听写、语音输入或其他语音 App 的场景。

```text
Windows 麦克风 → UU 端口直连 / 可信 TCP 隧道 → Mac → BlackHole 2ch → Mac 语音 App
```

MicBridge 只传声音，不做语音识别，不粘贴文字，不需要 API Key。它是独立的开源工具，与网易 UU 没有隶属或合作关系。

## 下载

[**打开 Releases 下载最新版**](https://github.com/Carooot/micbridge/releases/latest)

- **Windows**：`MicBridge-Windows-x64.zip`，解压后打开 `MicBridge.exe`。
- **Mac**：`MicBridge-macOS-universal.zip`，解压后打开 `MicBridge.app`。

Windows 10/11 x64，.NET Framework 4.8；macOS 13+，Apple Silicon / Intel 通用程序。需要在 Mac 单独安装 [BlackHole 2ch](https://github.com/ExistentialAudio/BlackHole)。本项目不捆绑声卡驱动。

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

### 3. Windows 连接，然后按住说话

打开 `MicBridge.exe`，选好麦克风，点 **连接 Mac**。

- 默认 **按住 F8**：打开麦克风并传输；**松开 F8**：释放麦克风，清掉未发送的声音，并通知 Mac 停止音频输出。
- 可以在连接前把按键改为 **F9 / F10**。部分笔记本需要配合 Fn 键。
- 连接后可以最小化。无需切出 UU 窗口，按键在后台生效。
- 待命时只发连接心跳，不采集麦克风。故障后停止并提示，恢复后手动重新连接。
- 单次按住最多 2 分钟；达到上限后必须松开，再按住继续。

这个按键只控制麦克风传输。识别、标点和文本输入由你使用的 Mac 语音功能决定。

## 从旧版升级

退出旧的 `UUMic.exe` 和“UU麦克风接收器”，然后同时换成这两个新版程序。UU 映射仍用 **18768**。旧版连接码协议与新版按住说话协议不兼容；不要让两个 Mac 接收器争用同一个端口。

## 常见问题

| 现象 | 检查方式 |
|---|---|
| 连接不上 | 确认 Mac 接收器运行、UU 映射已开启、两边端口都是 18768。 |
| 提示端口被占用 | 退出旧接收器或另一份 MicBridge，再重新打开。 |
| Windows 音量条不动 | 按住所选按键；检查所选麦克风，并在 Windows 隐私设置中允许桌面应用访问麦克风。 |
| Mac 音量条动，App 没反应 | 在该 App 中选 BlackHole 2ch，并按要求授予该 App 麦克风权限。 |
| 黑洞声卡不可用 | 安装 BlackHole 2ch，按驱动要求完成重启，再点“重新检查声卡”。 |
| 快捷键在 UU 中无效 | 检查 Fn 模式；让 UU 和 MicBridge 使用一致的正常用户权限；也可以断开后换 F9/F10。 |
| 网络断开或积压 | 工具会关闭麦克风。恢复隧道后手动重新连接。 |

## 隐私与信任范围

- 不落盘保存音频，不调用识别云服务，不收集遥测，不读写剪贴板。
- Windows 每次启动均处于断开状态，连接成功后仍保持麦克风关闭。
- 接收端只监听 **127.0.0.1**，不直接监听局域网或公网。
- 按设计**不做额外身份认证或应用层加密**，依赖你配置的 UU 或其他可信隧道。任何能访问该本地转发端口的程序都可以连接；不要另行公开映射到公网。
- 48 kHz、16 bit、单声道 PCM，讲话时约 96 KB/s。网络排队和设备本身仍会带来少量延迟，不能承诺远端声音在物理松键瞬间零延迟停止。

## 构建与测试

只使用系统框架和 Python 标准库，不需要第三方运行库或识别模型。

### macOS

安装 Xcode Command Line Tools 和 Python 3，然后：

```sh
python3 scripts/build.py mac
python3 tests/integration.py
```

输出：`build/mac/MicBridge.app`。构建脚本同时生成 arm64 / x86_64 二进制并合并，运行协议和缓冲测试。

已安装 BlackHole 时，可额外验证真实音频输入和松开后的静音：

```sh
python3 tests/integration.py --audio
```

此测试向 BlackHole 发送生成的测试音，并从 BlackHole 输入读取强度与频率，不使用物理麦克风，不保存录音。请在没有其他应用向 BlackHole 输出声音时运行。

### Windows

安装 Python 3，使用 .NET Framework 4.8 自带的编译器：

```powershell
python scripts/build.py windows
```

输出：`build/windows/MicBridge.exe`。脚本也会运行队列、按住/释放控制、协议边界和工作线程退出测试。

### 打包

```sh
python scripts/package.py mac
python scripts/package.py windows
```

ZIP 输出在 `dist/`。GitHub Actions 在提交和拉取请求时执行两端构建与测试；发布 Release 时，测试通过后自动附上两端安装包和校验文件。

## 实现与验证范围

Windows 使用 WinMM 录音、全局键盘钩子和一个有上限的音频队列；Mac 使用 Network.framework 接收、AudioQueue 输出到 BlackHole。协议说明见 [PROTOCOL.md](PROTOCOL.md)。

基础音频转发版本已由使用者在 Windows → UU → Mac 场景确认可用。当前按住说话版的自动化验证覆盖协议分片、待命、开始/停止、断线、队列清空和 BlackHole 回读；全局按键、不同 Windows 麦克风及长期弱网仍需要更多真实设备反馈。

## 开源协议

[MIT](LICENSE)。BlackHole 是单独安装的外部项目，遵循其自己的开源协议。反馈问题时请描述系统、麦克风、连接步骤和报错，避免上传私人录音或账号凭据。

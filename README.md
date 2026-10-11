# MicBridge · 远程麦克风

**用 Windows + UU 远程控制 Mac，照常唤起 Mac 语音输入，Windows 麦克风自动开始传输。**

本工具通过 **UU 远程的「端口直连 / 端口转发」**连接两端。UU 负责远程连接，MicBridge 负责传声音，豆包等 Mac 应用负责识别和输入文字。语音应用结束收音后，Windows 自动关麦；不用额外按 F8，不用给 MicBridge 配快捷键。

```text
Windows 麦克风 → UU 远程 TCP 端口直连 → Mac 接收器 → BlackHole 2ch → 语音输入
```

MicBridge 是独立的开源工具，与网易 UU 没有隶属或合作关系。

## 第一次用：只下载这一套

### [下载完整安装包 MicBridge-Setup.zip](https://github.com/Carooot/micbridge/releases/latest/download/MicBridge-Setup.zip)

一个压缩包已经包含：

| 文件 | 用途 |
|---|---|
| `MicBridge.exe` | Windows 程序，解压后直接打开，免安装 |
| `Mac/MicBridge-Mac-Setup.pkg` | Mac 图形化安装器，一起准备接收器和 BlackHole 2ch |
| `开始使用.txt` | 离线中文说明 |

不用在两台电脑分别搜索下载，也不用另外寻找声卡安装包或安装 Homebrew。**两台电脑都需要运行对应端程序**：在 Windows 下载整套包，用 UU 文件传输把 `Mac` 文件夹发到被控 Mac 即可。

已经装过的用户：[只下载 Windows](https://github.com/Carooot/micbridge/releases/latest/download/MicBridge-Windows-x64.zip) · [只下载 Mac 安装器](https://github.com/Carooot/micbridge/releases/latest/download/MicBridge-Mac-Setup.pkg) · [全部版本](https://github.com/Carooot/micbridge/releases)

## 首次设置

### 1. 在被控 Mac 安装

把包里的 `Mac` 文件夹传到 Mac，双击 `MicBridge-Mac-Setup.pkg`，按系统安装器提示完成。

- 接收器会安装到“应用程序”。
- 没有 BlackHole 2ch：安装器一起安装官方声卡，按系统提示重启后再继续。
- 已有 BlackHole 2ch：自动跳过该组件，不覆盖声卡。
- 安装需要 macOS 管理员确认。升级前先退出旧 MicBridge，首次安装前保存工作。

从“应用程序”打开 **MicBridge**，点一次 **设为语音输入**。这会把 Mac 系统默认麦克风改为 BlackHole 2ch，不改变扬声器。如果语音应用自己指定了麦克风，也要在该应用内选择 BlackHole 2ch。

### 2. 在 Windows 的 UU 远程开启端口直连

在 UU 中选中要控制的 Mac，找到 **端口直连 / 端口转发**，添加并启用：

| 项目 | 填写内容 |
|---|---|
| 协议 | **TCP** |
| 目标地址 | **127.0.0.1** |
| Mac 目标端口 | **18768** |
| Windows 本地端口 | **18768** |

这一步由 UU 建立两端通道，需要首次手动配置。MicBridge 不替你登录 UU、选择被控设备或开放公网端口。功能名称及位置以你的 UU 版本为准。

### 3. 连接，照常说话

Windows 双击 **MicBridge.exe**，点 **连接 Mac**。麦克风可在“设置”中更换；“首次使用 / 连接帮助”内也有上面的安装步骤和端口表。

照常用自己的快捷键或按钮唤起 Mac 语音输入。MicBridge 检测到所选应用开始读取 BlackHole 后，才打开 Windows 麦克风；应用停止读取后，自动关闭麦克风并清空播放队列。

以后使用只需打开两端 MicBridge，保持 UU 映射开启，连接一次。

## 设置与使用

- **Mac 跟随应用**：检测到豆包输入法时默认跟随豆包，也可换成其他语音应用或所有使用 BlackHole 的应用。列表来自当前音频进程，没出现时先打开目标应用，再重新打开设置。
- **暂停联动**：两端都可暂停。Windows 窗口、程序文件和任务栏托盘使用统一的麦克风图标，托盘也可恢复窗口或退出。
- **后台待命**：默认关闭窗口后仍待命，可在设置关闭。待命不采集 Windows 麦克风。
- **断线 / 锁屏 / 休眠**：停止采集，恢复后手动重新连接。
- **持续收音的应用**：联动依据是实际输入流。应用若一直占用 BlackHole，MicBridge 也会持续传输，可只跟随需要的应用或点“暂停联动”。

## 系统要求与升级

Windows 10/11 x64 + .NET Framework 4.8；macOS **14.2 或更新**，Apple Silicon / Intel 通用。

**1.2 与 1.1 的音频协议兼容。** 已经跑通 1.1 的用户可以直接更新 Windows 界面；从 1.0 或早期原型升级则需要同时更新两端。端口一直为 **18768**，不要同时打开两个 Mac 接收器。

整合安装器暂未商业签名或 Apple 公证。如果系统阻止，请核对下载来源和校验值，在系统“隐私与安全性”中仅允许这个安装器，或自行构建；不要关闭系统整体安全保护。BlackHole 上游组件的签名不等于整个 MicBridge 安装包已公证。

## 常见问题

| 现象 | 怎么处理 |
|---|---|
| 连不上 Mac | 确认接收器已打开、UU 映射已启用、两边都是 18768。Windows 窗口的“连接帮助”有填写值。 |
| 提示端口被占用 | 退出另一份旧接收器，再打开新版。 |
| 已安装但找不到 BlackHole | 首次装驱动后按提示重启，再打开 MicBridge；设置里可重新检查声卡。 |
| Windows 音量条不动 | 先唤起 Mac 语音输入，确认跟随了正确应用、未暂停；在 Windows 隐私设置允许桌面应用使用麦克风。 |
| Mac 收到声音但没出文字 | 确认语音应用使用 BlackHole，且已授予它麦克风权限。识别和文字输入由该应用负责。 |
| 结束说话还在传输 | 检查应用是否仍在收音；MicBridge 不根据安静与否猜测结束，可点暂停。 |
| 网络中断或积压 | 麦克风会关闭，恢复隧道后手动重新连接。 |

## 隐私与连接范围

不保存录音，不调用识别云服务，不收集遥测，不读写剪贴板，不监听键盘。Windows 启动时保持断开，连接后只按 Mac 请求和暂停状态开麦。

接收器只监听 **127.0.0.1**，不监听局域网或公网。不使用连接码，也不增加应用层加密，访问控制依赖 UU 或你自己配置的可信 TCP 隧道；任何能访问本地转发端口的程序都可能连接，不要把它另行公开到公网。

音频为 48 kHz / 16 bit / 单声道 PCM，传输时约 96 KB/s。检测、网络及麦克风启动会带来少量延迟；停止检测包含约 250 ms 防抖，不提前录音。

## 构建与验证

应用只使用系统框架，不捆绑识别模型或第三方运行库。

```sh
# Mac：Xcode Command Line Tools + Python 3
python3 scripts/build.py mac
python3 tests/integration.py
python3 scripts/package.py mac
python3 scripts/mac_installer.py
python3 tests/installer.py

# 有 BlackHole 的 Mac：真实输入回读与自动开关
python3 tests/integration.py --audio
python3 tests/automatic.py

# Windows：Python 3 + .NET Framework 4.8
python scripts/build.py windows
python scripts/package.py windows
```

`mac_installer.py` 从官方固定版本下载并校验 BlackHole，将独立组件、许可和对应源码加入安装器。`tests/installer.py` 默认只检查文件与组件选择；`--install` 仅允许在 GitHub 的临时 Mac 测试机中运行，验证从未安装到已安装后的自动跳过，不自动重启。

原始图标可在 Mac 用 `python3 scripts/icons.py` 重新生成。Windows 构建会输出真实窗口预览，覆盖主窗口、设置和连接帮助。

GitHub Actions 执行两端构建、协议测试、Windows 实际 TCP 控制、Mac 安装测试及整套包核对。发布 Release 后自动上传文件和 SHA-256 校验。协议见 [PROTOCOL.md](PROTOCOL.md)。

自动跟随版已由使用者在 Windows → UU → Mac → 豆包场景确认跑通。不同声卡、显示缩放、语音应用和长期弱网仍欢迎反馈。

## 开源许可

MicBridge 和原创图标采用 [MIT](LICENSE)。随 Mac 安装器分发的 BlackHole 是独立的 GPL-3.0 项目，其许可与完整对应源码一起提供，详见 [THIRD-PARTY.md](THIRD-PARTY.md)。

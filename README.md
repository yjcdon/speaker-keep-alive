# 音箱静音保活 · Speaker Keep Alive

**音箱静音保活（Speaker Keep Alive）是 Windows 10 的音箱保活托盘工具，通过向指定音箱持续发送静音音频，尝试避免蓝牙音箱闲置后自动关机。**

适合音箱在停止播放一段时间后自动关闭、下次使用需要重新开机的场景。程序固定保活你选定的音箱，切换到耳机或其他默认输出后仍继续运行。

[下载最新版 SpeakerKeepAlive.exe](https://github.com/yjcdon/speaker-keep-alive/releases/latest) · [版本记录](https://github.com/yjcdon/speaker-keep-alive/releases) · [反馈问题](https://github.com/yjcdon/speaker-keep-alive/issues)

## 下载与使用

运行环境：**Windows 10、.NET Framework 4.8**。下载的 EXE 可直接运行，无需安装程序。

1. 在 [Releases](https://github.com/yjcdon/speaker-keep-alive/releases/latest) 下载 `SpeakerKeepAlive.exe`，放到准备长期保留的文件夹并双击运行。
2. 连接音箱，右键任务栏通知区域中的程序图标，点击“选择保活音箱”，选定目标设备。
3. 关闭状态窗口后，程序继续在托盘运行。通过托盘菜单可查看“运行状态”、暂停或继续保活、设置“开机自启”以及退出。

**首次使用请验证音箱兼容性：**停止其他音频播放，保持程序运行并观察至少 30 分钟；如果音箱通常更晚才关机，请覆盖其平时的闲置关机时间。纯静音能否阻止自动关机取决于音箱固件，“静音流正在运行”表示 Windows 正在接受音频数据，实际保活效果以音箱表现为准。

## 核心功能

- **固定目标音箱：**保存所选设备，切换 Windows 默认音频输出时继续保活原音箱。
- **断线后恢复：**设备断开时等待重连，重新连接后自动重试。
- **托盘控制：**在运行状态窗口查看目标音箱、当前状态和连续运行时长，随时暂停或继续。
- **开机自启：**支持当前用户登录后自动运行。
- **全零静音：**通过 WASAPI 共享模式发送全零音频数据，保持系统音量设置，不使用麦克风。
- **单文件运行：**使用发布版 EXE 即可运行，图标等所需资源已嵌入程序。运行中使用银灰色音箱图标，暂停时使用灰色图标。

## 常见问题

### 连接音箱后，为什么在列表中找不到？

设备选择列表展示 Windows 当前处于活动状态的音频播放设备。请先确认音箱已连接，并在 Windows 声音设置中作为可用输出设备出现，再重新打开“选择保活音箱”。

### 状态显示运行中，音箱还是自动关机？

部分音箱固件按实际声音能量判断闲置，即使持续收到全零音频也可能关机。可按上面的首次验证步骤确认设备表现，并在 Issue 中提供音箱型号、Windows 版本、程序版本和观察到的关机时间。

### 设置保存在哪里？移动 EXE 后需要做什么？

目标音箱的设备 ID 和名称保存在当前用户注册表 `HKCU\Software\SpeakerKeepAlive`。开机自启使用当前用户的 Windows 启动项；移动 EXE 后，请运行新位置的程序，重新设置“开机自启”，使其指向新路径。

## 从源码编译

项目使用 C#、Windows Forms 和 WASAPI。构建需要 Windows 与 .NET Framework 4.8，脚本调用系统自带的 C# 编译器，无需 Visual Studio 或额外 SDK。

克隆仓库并进入目录，先退出正在运行的程序，再在 PowerShell 中执行：

```powershell
git clone https://github.com/yjcdon/speaker-keep-alive.git
cd speaker-keep-alive
& '.\build.ps1'
```

生成的 `SpeakerKeepAlive.exe` 位于仓库根目录。版本变化见 [Releases](https://github.com/yjcdon/speaker-keep-alive/releases)。

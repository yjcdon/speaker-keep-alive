# 音箱静音保活

适用于 Windows 10 的轻量 C# 托盘工具，通过 WASAPI 共享模式向所选音箱持续发送全零静音数据。

## 功能

- 只保活选定的音箱，不随系统默认输出切换。
- 音箱断线后等待重连，连接恢复后自动重试。
- 托盘运行、状态窗口、暂停与继续、当前用户开机自启。
- 不调整音量，不生成超声波、低频声或噪声，不使用麦克风。
- 免安装，运行只需要 `SpeakerKeepAlive.exe`。

纯静音能否阻止自动关机取决于音箱固件。状态窗口显示静音流运行，仅代表 Windows 正在接受数据；请在停止其他播放的情况下观察至少 30 分钟，确认实际效果。

## 使用

1. 按下方步骤编译，将生成的 `SpeakerKeepAlive.exe` 放在准备长期保留的位置，双击运行。
2. 通过托盘菜单“选择保活音箱”选择目标。
3. 关闭状态窗口后继续在托盘运行；右键托盘图标可打开状态、暂停、设置自启或退出。

音箱选择保存在当前用户的 `HKCU\Software\SpeakerKeepAlive`。移动程序后，需在新位置重新设置开机自启。

## 编译

需要 Windows 与 .NET Framework 4.8，使用系统自带的 C# 编译器，无需 Visual Studio 或额外 SDK。

先退出正在运行的程序，在 PowerShell 中执行：

```powershell
& '.\源码\build.ps1'
```

生成文件位于项目根目录。仓库仅包含源码、编译所需资源及本 README，不包含编译产物。当前版本为 **1.2.1**，包括托盘菜单打开状态窗口的时机修复。

## 图标许可

图标基于 [Bootstrap Icons speaker-fill](https://icons.getbootstrap.com/icons/speaker-fill/)，第三方许可见 [源码/assets/Bootstrap-Icons-LICENSE.txt](源码/assets/Bootstrap-Icons-LICENSE.txt)。该许可仅涵盖相应第三方图标。

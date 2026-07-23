# AppToWallpaper

[English](README.md)

AppToWallpaper 可以把一个 Windows 应用程序设置成动态桌面壁纸。

它主要面向陪伴型游戏和适合长时间放在后台运行的氛围应用。应用处于壁纸模式时，鼠标和键盘仍然与 Windows 桌面交互：你可以右键桌面、选择文件、拖动桌面图标，而不会误操作游戏。需要操作游戏时，可以通过快捷键暂时把它切回普通窗口，操作完成后再返回壁纸模式。

这个项目最初是为了让 [梅莫莉：治愈物语（Memory of Memorie: A Chill Story）](https://store.steampowered.com/app/4337440/_/) 这样的陪伴游戏能够一直留在桌面上，而不是占用一个普通的应用窗口。

项目的最初灵感和 Windows 壁纸实现思路来自 [GameCG-to-Wallpaper](https://github.com/awp9835/GameCG-to-Wallpaper)。

开始之前请先把目标游戏调整成全屏。

当然也可以先开游戏再开这个小程序。

## 运行要求

- Windows 10 或 Windows 11
- 使用标准的 Windows Explorer 桌面
- 目标应用或游戏能够打开一个可见窗口

## 快速开始

1. 将 `AppToWallpaper.exe` 放在一个单独的文件夹中。
2. 运行 `AppToWallpaper.exe`。
3. 第一次运行时，在弹出的窗口中选择要作为壁纸的应用或游戏 EXE。
4. 程序会在自身旁边创建 `config.json`，并开始管理所选应用。
5. 按 `Ctrl+F10` 切出壁纸模式，此时可以正常操作应用或游戏。
6. 按 `Ctrl+F12` 返回壁纸模式。

如果配置中的 EXE 被移动或删除，下次启动 AppToWallpaper 时会再次弹出应用选择窗口。

AppToWallpaper 会安静地在后台运行，不会显示控制台黑框。需要停止时，可以关闭被管理的应用，或者在任务管理器中结束 `AppToWallpaper`。程序只允许同时运行一个实例。

## 配置文件

自动生成的 `config.json` 如下：

```json
{
  "PathToExe": "C:\\Path\\To\\Game.exe",
  "AutoSetWallpaper": true,
  "ExitWallpaperHotkey": "Ctrl+F10",
  "AutoReturnSeconds": 0,
  "ReturnWallpaperHotkey": "Ctrl+F12",
  "AutoStartProgram": true,
  "StartupDelaySeconds": 7
}
```

修改配置后，需要关闭并重新启动 AppToWallpaper 才会生效。

### `PathToExe`

要作为壁纸运行的应用或游戏 EXE 完整路径。

如果路径不存在或已经失效，AppToWallpaper 会要求重新选择一个 EXE。

### `AutoSetWallpaper`

- `true`：找到目标窗口后，自动将它设置为壁纸。
- `false`：找到目标窗口后保持普通窗口形态，直到按下返回壁纸快捷键。

### `ExitWallpaperHotkey`

切出壁纸模式的快捷键。切出后，应用会恢复成可以正常操作的窗口。

默认值：`Ctrl+F10`

### `AutoReturnSeconds`

应用切出壁纸模式后，经过多少秒自动返回壁纸模式。

- `0`：不自动返回。
- `1` 到 `3600`：经过指定秒数后自动返回。

无论是否开启自动返回，都可以随时使用 `ReturnWallpaperHotkey` 手动返回。

### `ReturnWallpaperHotkey`

手动返回壁纸模式的快捷键。

默认值：`Ctrl+F12`

### `AutoStartProgram`

- `true`：AppToWallpaper 自动启动配置中的 EXE。
- `false`：不启动任何程序，只查找当前已经运行的同名进程。

如果你希望自己启动游戏，或者必须通过其他启动器启动游戏，可以将它设为 `false`。

### `StartupDelaySeconds`

自动启动应用后，等待多少秒再开始寻找真正的游戏窗口。

默认值：`7`

这个延时对 Steam 游戏尤其重要。部分 Steam 游戏从 EXE 直接启动时，最初的进程会很快关闭，然后由 Steam 重新启动一个新进程。等待一段时间后再检测，可以避免 AppToWallpaper 连接到那个马上就会退出的临时进程。

此配置只在 `AutoStartProgram` 为 `true` 时生效。

## 快捷键格式

快捷键中的按键使用 `+` 分隔，例如：

```text
Ctrl+F10
Alt+F9
Ctrl+Shift+F12
```

支持的修饰键包括 `Ctrl`、`Alt`、`Shift` 和 `Win`，最后一个按键可以使用功能键、字母或数字。切出和返回快捷键不能设置成相同组合。

## 常见问题

### 启动后没有反应

请先在任务管理器中确认 AppToWallpaper 是否已经运行。程序只允许同时运行一个实例。

### 找不到应用窗口

请确认所选程序已经打开了一个可见窗口。启动延时结束后，AppToWallpaper 最多会继续等待一分钟。如果启动器和最终游戏使用不同的 EXE 名称，请选择最终游戏窗口所属的 EXE。

### Steam 游戏启动后退出，又重新启动

可以适当增加 `StartupDelaySeconds`。对于由启动器管理的游戏，可以先尝试设置为 7 到 15 秒。

### 无法正常操作桌面

AppToWallpaper 依赖标准的 Windows Explorer 桌面。第三方桌面替代软件或经过大量修改的 Explorer 环境可能会有不同表现。

### 日志文件

排查问题时，可以查看：

```text
%LocalAppData%\AppToWallpaper\AppToWallpaper.log
```

日志文件过大时会自动轮换。

## 致谢

- 灵感来自 [awp9835/GameCG-to-Wallpaper](https://github.com/awp9835/GameCG-to-Wallpaper)
- 为 [梅莫莉：治愈物语（Memory of Memorie: A Chill Story）](https://store.steampowered.com/app/4337440/_/) 以及类似的陪伴游戏而制作

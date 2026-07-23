# AppToWallpaper

[中文说明](README.zh.md)

AppToWallpaper turns a Windows application into a live desktop wallpaper.

It is intended for companion games and ambient applications that you may want to leave running in the background. While the application is in wallpaper mode, your mouse and keyboard continue to work with the Windows desktop: you can right-click the desktop, select files, and drag desktop icons without interacting with the game. A shortcut temporarily brings the application back as a normal, interactive window whenever you want to use it.

This project was created for companion games such as [Memory of Memorie: A Chill Story](https://store.steampowered.com/app/4337440/_/), so they can remain part of the desktop instead of occupying a normal application window.

The original idea and Windows wallpaper approach were inspired by [GameCG-to-Wallpaper](https://github.com/awp9835/GameCG-to-Wallpaper).

Make sure the game already set to full screen mode.

You can also open the game first, and then run this tool.

## Requirements

- Windows 10 or Windows 11
- The standard Windows Explorer desktop
- A desktop application or game with a visible window

## Getting Started

1. Put `AppToWallpaper.exe` in its own folder.
2. Run `AppToWallpaper.exe`.
3. On the first run, choose the application or game executable (`.exe`) that you want to use.
4. AppToWallpaper creates `config.json` beside itself and starts managing the selected application.
5. Press `Ctrl+F10` to bring the application out of wallpaper mode and interact with it normally.
6. Press `Ctrl+F12` to return it to wallpaper mode.

If the configured executable is moved or deleted, the application picker opens again the next time AppToWallpaper starts.

AppToWallpaper runs quietly in the background and does not open a console window. To stop it, close the managed application or end `AppToWallpaper` from Task Manager. Only one instance of AppToWallpaper can run at a time.

## Configuration

The generated `config.json` looks like this:

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

Close and restart AppToWallpaper after changing the configuration.

### `PathToExe`

The full path to the application or game executable.

If the path is missing or no longer valid, AppToWallpaper asks you to select another executable.

### `AutoSetWallpaper`

- `true`: Put the application into wallpaper mode as soon as its window is found.
- `false`: Leave it as a normal window until the return-to-wallpaper shortcut is pressed.

### `ExitWallpaperHotkey`

The shortcut that brings the application out of wallpaper mode so you can interact with it normally.

Default: `Ctrl+F10`

### `AutoReturnSeconds`

How many seconds AppToWallpaper waits after bringing the application out before automatically returning it to wallpaper mode.

- `0`: Do not return automatically.
- `1` to `3600`: Return after this many seconds.

You can always return manually with `ReturnWallpaperHotkey`.

### `ReturnWallpaperHotkey`

The shortcut that manually returns the application to wallpaper mode.

Default: `Ctrl+F12`

### `AutoStartProgram`

- `true`: Start the configured executable automatically.
- `false`: Do not start anything; only look for an already running process with the same executable name.

This is useful if you prefer to start the game yourself or through another launcher.

### `StartupDelaySeconds`

How many seconds to wait after automatically starting the application before looking for its real window.

Default: `7`

This delay is especially important for Steam games. When some games are launched directly, the first process may close quickly and then be started again by Steam. Waiting before detection prevents AppToWallpaper from attaching to that short-lived first process.

This setting only applies when `AutoStartProgram` is `true`.

## Shortcut Format

Shortcuts use a `+` between keys, for example:

```text
Ctrl+F10
Alt+F9
Ctrl+Shift+F12
```

Supported modifier keys include `Ctrl`, `Alt`, `Shift`, and `Win`. Function keys, letters, and numbers can be used as the final key. The two configured shortcuts must be different.

## Troubleshooting

### Nothing happens after starting

Check whether AppToWallpaper is already running in Task Manager. The application allows only one running instance.

### The application window cannot be found

Make sure the selected program has opened a visible window. AppToWallpaper waits for up to one minute after the startup delay. If a launcher and the actual game use different executable names, select the executable that owns the final game window.

### A Steam game closes and starts again

Increase `StartupDelaySeconds`. Values between 7 and 15 seconds are usually a good starting point for launcher-managed games.

### Desktop interaction does not work as expected

AppToWallpaper relies on the standard Windows Explorer desktop. Third-party desktop replacements or heavily customized Explorer environments may behave differently.

### Log file

When diagnosing a problem, check:

```text
%LocalAppData%\AppToWallpaper\AppToWallpaper.log
```

The log is automatically rotated when it becomes large.

## Acknowledgements

- Inspired by [awp9835/GameCG-to-Wallpaper](https://github.com/awp9835/GameCG-to-Wallpaper)
- Created with [Memory of Memorie: A Chill Story](https://store.steampowered.com/app/4337440/_/) and similar companion games in mind

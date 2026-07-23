using System.Diagnostics;
using System.Text.Json;

namespace AppToWallpaper;

internal static class Program
{
    private const int WindowDetectionTimeoutSeconds = 60;
    private const int PollIntervalMilliseconds = 50;
    private const string InstanceSemaphoreName = "Local\\AppToWallpaper.SingleInstance";

    [STAThread]
    public static async Task<int> Main()
    {
        Log.Initialize();
        try
        {
            using var instanceSemaphore = new Semaphore(1, 1, InstanceSemaphoreName);
            if (!instanceSemaphore.WaitOne(0))
            {
                AppUi.Show("AppToWallpaper 已经在运行。", true);
                return 0;
            }

            try
            {
                return await RunAsync();
            }
            finally
            {
                instanceSemaphore.Release();
            }
        }
        catch (Exception exception)
        {
            Log.Error("发生未处理异常。", exception);
            AppUi.Show($"程序发生未处理错误：\n\n{exception.Message}\n\n详细信息已写入日志。", true);
            return 1;
        }
    }

    private static async Task<int> RunAsync()
    {
        using var cancellation = new CancellationTokenSource();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => cancellation.Cancel();
        Log.Info("程序启动。");

        AppConfig config;
        try
        {
            config = ConfigFile.LoadOrCreate(out var created);
            if (created || !IsValidApplicationPath(config.PathToExe))
            {
                var selectedPath = AppUi.SelectApplication(config.PathToExe);
                if (selectedPath == null)
                {
                    Log.Info(created
                        ? "首次运行时取消了应用程序选择。"
                        : $"配置的应用程序不存在，用户取消了重新选择: {config.PathToExe}");
                    return 0;
                }

                config.PathToExe = selectedPath;
                ConfigFile.Save(config);
                Log.Info($"已选择目标程序: {selectedPath}");
            }
            config.Validate();
        }
        catch (Exception exception) when (exception is IOException or JsonException or ConfigException)
        {
            Log.Error("配置加载失败。", exception);
            AppUi.Show($"配置错误：\n\n{exception.Message}", true);
            return 1;
        }

        Hotkey exitHotkey;
        Hotkey returnHotkey;
        try
        {
            exitHotkey = Hotkey.Parse(config.ExitWallpaperHotkey);
            returnHotkey = Hotkey.Parse(config.ReturnWallpaperHotkey);
            if (exitHotkey == returnHotkey)
                throw new ConfigException("切出壁纸和切回壁纸的快捷键不能相同。");
        }
        catch (ConfigException exception)
        {
            Log.Error("快捷键配置无效。", exception);
            AppUi.Show($"配置错误：\n\n{exception.Message}", true);
            return 1;
        }

        var processName = Path.GetFileNameWithoutExtension(config.PathToExe);
        if (config.AutoStartProgram)
        {
            try
            {
                var fullPath = Path.GetFullPath(config.PathToExe);
                using var initialProcess = Process.Start(new ProcessStartInfo(fullPath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(fullPath)!
                });
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Log.Error("无法启动目标程序。", exception);
                AppUi.Show($"无法启动目标程序：\n\n{exception.Message}", true);
                return 1;
            }

            Log.Info($"已启动目标程序，等待 {config.StartupDelaySeconds} 秒后检测: {config.PathToExe}");
            if (!await DelayAsync(TimeSpan.FromSeconds(config.StartupDelaySeconds), cancellation.Token))
                return 0;
        }
        else
        {
            Log.Info("自动启动已关闭，仅查找当前运行的同名进程。");
        }

        Log.Info($"正在查找进程 {processName} 的主窗口，超时 {WindowDetectionTimeoutSeconds} 秒。");
        var target = await WindowFinder.FindAsync(processName, TimeSpan.FromSeconds(WindowDetectionTimeoutSeconds), cancellation.Token);
        if (target == 0)
        {
            if (!cancellation.IsCancellationRequested)
            {
                var message = $"未找到进程 {processName} 的可见顶层窗口。";
                Log.Error(message);
                AppUi.Show(message, true);
            }
            return cancellation.IsCancellationRequested ? 0 : 1;
        }

        NativeMethods.GetWindowThreadProcessId(target, out var processId);
        Log.Info($"找到目标窗口: PID {processId}, HWND 0x{target:X}");

        var wallpaper = new WallpaperController(target);
        var isWallpaper = false;
        var recoverWallpaper = false;
        long? detachedAt = null;
        var lastRecoveryAttempt = 0L;
        var exitWasPressed = false;
        var returnWasPressed = false;

        try
        {
            if (config.AutoSetWallpaper)
            {
                if (!wallpaper.TryAttach(out var error))
                {
                    Log.Error($"无法设置为壁纸: {error}");
                    AppUi.Show($"无法设置为壁纸：\n\n{error}", true);
                    return 1;
                }
                isWallpaper = true;
            }

            Log.Info($"管理已开始。切出: {config.ExitWallpaperHotkey}; 切回: {config.ReturnWallpaperHotkey}; " +
                     $"自动切回: {(config.AutoReturnSeconds == 0 ? "关闭" : $"{config.AutoReturnSeconds} 秒")}");

            while (!cancellation.IsCancellationRequested && NativeMethods.IsWindow(target))
            {
                var exitIsPressed = exitHotkey.IsPressed();
                var returnIsPressed = returnHotkey.IsPressed();

                if (exitIsPressed && !exitWasPressed && (isWallpaper || recoverWallpaper))
                {
                    if (isWallpaper)
                        wallpaper.Detach(true);
                    isWallpaper = false;
                    recoverWallpaper = false;
                    detachedAt = Stopwatch.GetTimestamp();
                    Log.Info("已切出壁纸模式。");
                }
                else if (returnIsPressed && !returnWasPressed && !isWallpaper)
                {
                    if (wallpaper.TryAttach(out var error))
                    {
                        isWallpaper = true;
                        recoverWallpaper = false;
                        detachedAt = null;
                        Log.Info("已切回壁纸模式。");
                    }
                    else
                    {
                        recoverWallpaper = true;
                        Log.Error($"切回失败，将继续重试: {error}");
                    }
                }

                if (isWallpaper && !wallpaper.Maintain())
                {
                    wallpaper.Detach(false);
                    isWallpaper = false;
                    recoverWallpaper = true;
                    lastRecoveryAttempt = Stopwatch.GetTimestamp();
                    Log.Error("Explorer 桌面层已变化，正在等待恢复壁纸。");
                }

                if (!isWallpaper && detachedAt.HasValue && config.AutoReturnSeconds > 0 &&
                    Stopwatch.GetElapsedTime(detachedAt.Value).TotalSeconds >= config.AutoReturnSeconds)
                {
                    detachedAt = null;
                    recoverWallpaper = true;
                }

                if (recoverWallpaper && !isWallpaper &&
                    Stopwatch.GetElapsedTime(lastRecoveryAttempt).TotalSeconds >= 1)
                {
                    lastRecoveryAttempt = Stopwatch.GetTimestamp();
                    if (wallpaper.TryAttach(out _))
                    {
                        isWallpaper = true;
                        recoverWallpaper = false;
                        Log.Info("已恢复壁纸模式。");
                    }
                }

                exitWasPressed = exitIsPressed;
                returnWasPressed = returnIsPressed;
                if (!await DelayAsync(TimeSpan.FromMilliseconds(PollIntervalMilliseconds), cancellation.Token))
                    break;
            }

            Log.Info(NativeMethods.IsWindow(target) ? "管理已停止。" : "目标窗口已关闭。");
            return 0;
        }
        finally
        {
            wallpaper.Detach(false);
            Log.Info("程序退出，已执行桌面恢复。");
        }
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static bool IsValidApplicationPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
               File.Exists(path);
    }
}

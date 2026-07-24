using System.Runtime.InteropServices;

namespace AppToWallpaper;

internal sealed class WallpaperController(nint target)
{
    private enum DesktopLayout
    {
        WorkerChildOfProgman,
        SplitWorker
    }

    private DesktopLayout _layout;
    private nint _progman;
    private nint _defView;
    private nint _iconsWorker;
    private nint _wallpaperWorker;
    private bool _attached;
    private bool _stateCaptured;
    private nint _originalParent;
    private WindowRect _originalRect;
    private int _originalShowCommand;
    private nint _originalStyle;

    public bool TryAttach(out string error)
    {
        error = "";
        if (!NativeMethods.IsWindow(target))
        {
            error = "目标窗口已经关闭。";
            return false;
        }
        if (!TryInitializeDesktop(out error))
            return false;

        if (!_stateCaptured)
        {
            _originalParent = NativeMethods.GetParent(target);
            NativeMethods.GetWindowRect(target, out _originalRect);
            _originalShowCommand = NativeMethods.IsZoomed(target) ? NativeMethods.SW_MAXIMIZE : NativeMethods.SW_RESTORE;
            _originalStyle = NativeMethods.GetWindowLongPtr(target, NativeMethods.GWL_STYLE);
            _stateCaptured = true;
        }

        NativeMethods.ShowWindow(target, NativeMethods.SW_RESTORE);
        var childStyle = new nint((_originalStyle.ToInt64() | NativeMethods.WS_CHILD) & ~NativeMethods.WS_POPUP);
        NativeMethods.SetLastError(0);
        NativeMethods.SetWindowLongPtr(target, NativeMethods.GWL_STYLE, childStyle);
        var styleError = Marshal.GetLastPInvokeError();
        if (styleError != 0)
        {
            error = $"修改目标窗口样式失败，Win32 错误 {styleError}。";
            NativeMethods.ShowWindow(target, _originalShowCommand);
            return false;
        }

        NativeMethods.SetLastError(0);
        var previousParent = NativeMethods.SetParent(target, _wallpaperWorker);
        if (previousParent == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            error = $"SetParent 失败，Win32 错误 {Marshal.GetLastPInvokeError()}。";
            RestoreAfterFailedAttach();
            return false;
        }
        if (!ResizeToWorker())
        {
            error = "无法读取或应用 WorkerW 客户区尺寸。";
            RestoreAfterFailedAttach();
            return false;
        }
        Log.Info($"壁纸挂载成功: 路径={_layout}, 目标=0x{target:X}, WorkerW=0x{_wallpaperWorker:X}。");

        if (_layout == DesktopLayout.WorkerChildOfProgman)
        {
            NativeMethods.ShowWindow(_defView, NativeMethods.SW_HIDE);
            Thread.Sleep(0);
            NativeMethods.ShowWindow(_defView, NativeMethods.SW_SHOW);
        }

        _attached = true;
        return true;
    }

    public void Detach(bool activate)
    {
        if (!_attached)
            return;

        if (!NativeMethods.IsWindow(target))
        {
            _attached = false;
            return;
        }

        NativeMethods.SetParent(target, _originalParent);
        NativeMethods.SetWindowLongPtr(target, NativeMethods.GWL_STYLE, _originalStyle);
        if (_stateCaptured)
        {
            NativeMethods.SetWindowPos(target, 0, _originalRect.Left, _originalRect.Top,
                _originalRect.Right - _originalRect.Left, _originalRect.Bottom - _originalRect.Top,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
            NativeMethods.ShowWindow(target, _originalShowCommand);
        }
        if (activate)
            NativeMethods.SetForegroundWindow(target);
        _attached = false;
        Log.Info("已恢复目标窗口的父窗口、样式和位置。");
    }

    public bool Maintain()
    {
        if (!_attached)
            return false;
        if (!NativeMethods.IsWindow(target) || !NativeMethods.IsWindow(_progman) || !NativeMethods.IsWindow(_defView))
        {
            Log.Error($"壁纸桌面窗口失效: 目标={NativeMethods.IsWindow(target)}, " +
                      $"Progman={NativeMethods.IsWindow(_progman)}, DefView={NativeMethods.IsWindow(_defView)}。");
            return false;
        }

        var actualParent = NativeMethods.GetParent(target);

        if (_layout == DesktopLayout.WorkerChildOfProgman)
        {
            var childWorker = NativeMethods.FindWindowEx(_progman, 0, "WorkerW", "");
            if (childWorker == 0)
            {
                Log.Error("壁纸 WorkerW 已消失。");
                return false;
            }
            if (childWorker != _wallpaperWorker || actualParent != childWorker)
            {
                Log.Info($"桌面层发生变化，重新挂载 WorkerW: 0x{_wallpaperWorker:X} -> 0x{childWorker:X}。");
                _wallpaperWorker = childWorker;
                NativeMethods.SetParent(target, _wallpaperWorker);
                ResizeToWorker();
            }
            return NativeMethods.IsWindow(_wallpaperWorker);
        }

        var currentWorker = NativeMethods.FindWindowEx(0, _iconsWorker, "WorkerW", "");
        if (currentWorker == 0)
        {
            Log.Error("桌面图标层后方的壁纸 WorkerW 已消失。");
            return false;
        }
        if (currentWorker != _wallpaperWorker || NativeMethods.GetParent(target) != currentWorker)
        {
            Log.Info($"桌面层发生变化，重新挂载 WorkerW: 0x{_wallpaperWorker:X} -> 0x{currentWorker:X}。");
            _wallpaperWorker = currentWorker;
            NativeMethods.SetParent(target, _wallpaperWorker);
            ResizeToWorker();
        }
        return true;
    }

    private bool TryInitializeDesktop(out string error)
    {
        error = "";
        _progman = NativeMethods.FindWindowEx(0, 0, "Progman", "Program Manager");
        if (_progman == 0)
        {
            error = "找不到 Explorer 的 Progman 窗口。";
            return false;
        }

        SplitDesktopLayers();
        _defView = NativeMethods.FindWindowEx(_progman, 0, "SHELLDLL_DefView", "");
        if (_defView != 0)
        {
            _wallpaperWorker = NativeMethods.FindWindowEx(_progman, 0, "WorkerW", "");
            if (_wallpaperWorker != 0)
            {
                _layout = DesktopLayout.WorkerChildOfProgman;
                return true;
            }

            // Explorer can apply the split asynchronously. Give it one short
            // chance to move DefView before falling back to the newer layout.
            Thread.Sleep(100);
            if (TryFindSplitWorkers())
            {
                _layout = DesktopLayout.SplitWorker;
                return true;
            }

            _wallpaperWorker = NativeMethods.FindWindowEx(_progman, 0, "WorkerW", "");
            if (_wallpaperWorker == 0)
            {
                error = "无法分裂桌面层，且 Progman 下不存在可用的 WorkerW。";
                return false;
            }
            _layout = DesktopLayout.WorkerChildOfProgman;
            return true;
        }

        if (TryFindSplitWorkers())
        {
            _layout = DesktopLayout.SplitWorker;
            return true;
        }

        error = "找不到桌面图标层及其后方的壁纸 WorkerW。";
        return false;
    }

    private void SplitDesktopLayers()
    {
        NativeMethods.SendMessageTimeout(_progman, NativeMethods.WM_USER + 300, 0, 0,
            NativeMethods.SMTO_NORMAL, 1000, out _);
        NativeMethods.SendMessageTimeout(_progman, NativeMethods.WM_USER + 300, 0xD, 0,
            NativeMethods.SMTO_NORMAL, 1000, out _);
        NativeMethods.SendMessageTimeout(_progman, NativeMethods.WM_USER + 300, 0xD, 1,
            NativeMethods.SMTO_NORMAL, 1000, out _);
    }

    private bool TryFindSplitWorkers()
    {
        _iconsWorker = NativeMethods.FindWindowEx(0, 0, "WorkerW", "");
        while (_iconsWorker != 0)
        {
            _defView = NativeMethods.FindWindowEx(_iconsWorker, 0, "SHELLDLL_DefView", "");
            if (_defView != 0)
                break;
            _iconsWorker = NativeMethods.FindWindowEx(0, _iconsWorker, "WorkerW", "");
        }
        if (_defView == 0)
            return false;

        _wallpaperWorker = NativeMethods.FindWindowEx(0, _iconsWorker, "WorkerW", "");
        return _wallpaperWorker != 0;
    }

    private bool ResizeToWorker()
    {
        if (!NativeMethods.GetClientRect(_wallpaperWorker, out var clientRect))
            return false;

        return NativeMethods.SetWindowPos(target, 0, 0, 0,
            clientRect.Right - clientRect.Left, clientRect.Bottom - clientRect.Top,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW);
    }

    private void RestoreAfterFailedAttach()
    {
        NativeMethods.SetParent(target, _originalParent);
        NativeMethods.SetWindowLongPtr(target, NativeMethods.GWL_STYLE, _originalStyle);
        NativeMethods.SetWindowPos(target, 0, _originalRect.Left, _originalRect.Top,
            _originalRect.Right - _originalRect.Left, _originalRect.Bottom - _originalRect.Top,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
        NativeMethods.ShowWindow(target, _originalShowCommand);
    }
}

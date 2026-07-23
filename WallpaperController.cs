using System.Runtime.InteropServices;

namespace AppToWallpaper;

internal sealed class WallpaperController(nint target)
{
    private enum DesktopLayout
    {
        Windows11_24H2,
        Legacy
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
            _stateCaptured = true;
        }

        var parent = _layout == DesktopLayout.Windows11_24H2 ? _wallpaperWorker : _progman;
        NativeMethods.SetLastError(0);
        var previousParent = NativeMethods.SetParent(target, parent);
        if (previousParent == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            error = $"SetParent 失败，Win32 错误 {Marshal.GetLastPInvokeError()}。";
            return false;
        }

        if (_layout == DesktopLayout.Windows11_24H2)
        {
            NativeMethods.ShowWindow(_defView, NativeMethods.SW_HIDE);
            Thread.Sleep(0);
            NativeMethods.ShowWindow(_defView, NativeMethods.SW_SHOW);
        }
        else
        {
            NativeMethods.ShowWindow(_wallpaperWorker, NativeMethods.SW_HIDE);
        }

        _attached = true;
        return true;
    }

    public void Detach(bool activate)
    {
        if (!_attached)
            return;

        if (_layout == DesktopLayout.Legacy && NativeMethods.IsWindow(_wallpaperWorker))
            NativeMethods.ShowWindow(_wallpaperWorker, NativeMethods.SW_SHOW);

        if (!NativeMethods.IsWindow(target))
        {
            _attached = false;
            return;
        }

        NativeMethods.SetParent(target, _originalParent);
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
    }

    public bool Maintain()
    {
        if (!_attached || !NativeMethods.IsWindow(target) || !NativeMethods.IsWindow(_progman) || !NativeMethods.IsWindow(_defView))
            return false;

        if (_layout == DesktopLayout.Windows11_24H2)
        {
            var currentWorker = NativeMethods.FindWindowEx(_progman, 0, "WorkerW", "");
            if (currentWorker == 0)
                return false;
            if (currentWorker != _wallpaperWorker)
            {
                _wallpaperWorker = currentWorker;
                NativeMethods.SetParent(target, _wallpaperWorker);
            }
            return NativeMethods.IsWindow(_wallpaperWorker);
        }

        if (NativeMethods.GetWindow(target, NativeMethods.GW_HWNDNEXT) == _defView)
            NativeMethods.SetParent(target, _progman);
        if (!NativeMethods.IsWindow(_wallpaperWorker))
            _wallpaperWorker = NativeMethods.FindWindowEx(0, _iconsWorker, "WorkerW", "");
        if (_wallpaperWorker == 0)
            return false;
        NativeMethods.ShowWindow(_wallpaperWorker, NativeMethods.SW_HIDE);
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

        NativeMethods.SendMessage(_progman, NativeMethods.WM_USER + 300, 0, 0);
        _defView = NativeMethods.FindWindowEx(_progman, 0, "SHELLDLL_DefView", "");
        if (_defView != 0)
        {
            _wallpaperWorker = NativeMethods.FindWindowEx(_progman, 0, "WorkerW", "");
            if (_wallpaperWorker == 0)
            {
                error = "检测到 Windows 11 24H2+ 桌面结构，但找不到壁纸 WorkerW。";
                return false;
            }
            _layout = DesktopLayout.Windows11_24H2;
            return true;
        }

        _iconsWorker = NativeMethods.FindWindowEx(0, 0, "WorkerW", "");
        while (_iconsWorker != 0)
        {
            _defView = NativeMethods.FindWindowEx(_iconsWorker, 0, "SHELLDLL_DefView", "");
            if (_defView != 0)
                break;
            _iconsWorker = NativeMethods.FindWindowEx(0, _iconsWorker, "WorkerW", "");
        }
        if (_defView == 0)
        {
            error = "找不到 SHELLDLL_DefView。请确认 Explorer 正常运行且桌面图标层可用。";
            return false;
        }

        _wallpaperWorker = NativeMethods.FindWindowEx(0, _iconsWorker, "WorkerW", "");
        if (_wallpaperWorker == 0)
        {
            error = "找不到桌面图标层后方的壁纸 WorkerW。";
            return false;
        }
        _layout = DesktopLayout.Legacy;
        return true;
    }
}

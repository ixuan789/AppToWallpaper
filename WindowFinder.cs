using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AppToWallpaper;

internal static class WindowFinder
{
    public static async Task<nint> FindAsync(string processName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        while (!cancellationToken.IsCancellationRequested && Stopwatch.GetElapsedTime(started) < timeout)
        {
            var processIds = new HashSet<uint>();
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        if (!process.HasExited)
                            processIds.Add((uint)process.Id);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }

            if (processIds.Count > 0)
            {
                var context = new WindowSearchContext(processIds);
                var handle = GCHandle.Alloc(context);
                try
                {
                    EnumerateWindows(GCHandle.ToIntPtr(handle));
                }
                finally
                {
                    handle.Free();
                }

                if (context.BestWindow != 0)
                    return context.BestWindow;
            }

            try
            {
                await Task.Delay(250, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int EnumWindow(nint window, nint parameter)
    {
        var context = (WindowSearchContext)GCHandle.FromIntPtr(parameter).Target!;
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (!context.ProcessIds.Contains(processId) || !NativeMethods.IsWindowVisible(window))
            return 1;
        if (!NativeMethods.GetWindowRect(window, out var rect))
            return 1;

        var area = (long)Math.Max(0, rect.Right - rect.Left) * Math.Max(0, rect.Bottom - rect.Top);
        if (area > context.BestArea)
        {
            context.BestArea = area;
            context.BestWindow = window;
        }
        return 1;
    }

    private static unsafe void EnumerateWindows(nint parameter)
    {
        NativeMethods.EnumWindows(&EnumWindow, parameter);
    }

    private sealed class WindowSearchContext(HashSet<uint> processIds)
    {
        public HashSet<uint> ProcessIds { get; } = processIds;
        public nint BestWindow { get; set; }
        public long BestArea { get; set; }
    }
}

using System.Runtime.InteropServices;

namespace AppToWallpaper;

internal static class AppUi
{
    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;
    private const uint MB_ICONINFORMATION = 0x00000040;
    private const uint MB_SETFOREGROUND = 0x00010000;
    private const uint OFN_PATHMUSTEXIST = 0x00000800;
    private const uint OFN_FILEMUSTEXIST = 0x00001000;
    private const uint OFN_EXPLORER = 0x00080000;
    private const uint OFN_NOCHANGEDIR = 0x00000008;
    private const int MaximumPathLength = 32_768;

    public static void Show(string message, bool isError)
    {
        NativeMethods.MessageBox(0, message, "AppToWallpaper",
            MB_OK | MB_SETFOREGROUND | (isError ? MB_ICONERROR : MB_ICONINFORMATION));
    }

    public static unsafe string? SelectApplication(string currentPath)
    {
        nint fileBuffer = 0;
        nint filter = 0;
        nint title = 0;
        nint initialDirectory = 0;
        nint defaultExtension = 0;

        try
        {
            fileBuffer = Marshal.AllocHGlobal(MaximumPathLength * sizeof(char));
            new Span<byte>((void*)fileBuffer, MaximumPathLength * sizeof(char)).Clear();
            filter = Marshal.StringToHGlobalUni("应用程序 (*.exe)\0*.exe\0\0");
            title = Marshal.StringToHGlobalUni("选择要设置为桌面壁纸的应用程序");
            defaultExtension = Marshal.StringToHGlobalUni("exe");

            try
            {
                var directory = Path.GetDirectoryName(currentPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    initialDirectory = Marshal.StringToHGlobalUni(directory);
            }
            catch (ArgumentException)
            {
                // An invalid configured path must not prevent the replacement picker from opening.
            }

            var dialog = new OpenFileName
            {
                StructSize = (uint)Marshal.SizeOf<OpenFileName>(),
                Filter = filter,
                FilterIndex = 1,
                File = fileBuffer,
                MaximumFileLength = MaximumPathLength,
                InitialDirectory = initialDirectory,
                Title = title,
                Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
                DefaultExtension = defaultExtension
            };

            if (NativeMethods.GetOpenFileName(ref dialog))
                return Marshal.PtrToStringUni(fileBuffer);

            var error = NativeMethods.CommDlgExtendedError();
            if (error != 0)
            {
                Log.Error($"文件选择窗口失败，Common Dialog 错误 0x{error:X}。");
                Show($"无法打开应用选择窗口。\n\n错误代码：0x{error:X}", true);
            }
            return null;
        }
        finally
        {
            if (defaultExtension != 0) Marshal.FreeHGlobal(defaultExtension);
            if (initialDirectory != 0) Marshal.FreeHGlobal(initialDirectory);
            if (title != 0) Marshal.FreeHGlobal(title);
            if (filter != 0) Marshal.FreeHGlobal(filter);
            if (fileBuffer != 0) Marshal.FreeHGlobal(fileBuffer);
        }
    }
}

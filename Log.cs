namespace AppToWallpaper;

internal static class Log
{
    private const long MaximumLogSize = 1_048_576;
    private static readonly object Sync = new();
    private static string? _filePath;

    public static void Initialize()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppToWallpaper");
            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, "AppToWallpaper.log");

            if (File.Exists(_filePath) && new FileInfo(_filePath).Length > MaximumLogSize)
                File.Move(_filePath, Path.Combine(directory, "AppToWallpaper.previous.log"), true);
        }
        catch
        {
            _filePath = null;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception == null ? message : $"{message} {exception}");
    }

    private static void Write(string level, string message)
    {
        if (_filePath == null)
            return;

        try
        {
            lock (Sync)
            {
                File.AppendAllText(_filePath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never interfere with wallpaper recovery.
        }
    }
}

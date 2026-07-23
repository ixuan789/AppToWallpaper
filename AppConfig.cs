using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppToWallpaper;

internal sealed class AppConfig
{
    public string PathToExe { get; set; } = "C:\\Path\\To\\Game.exe";
    public bool AutoSetWallpaper { get; set; } = true;
    public string ExitWallpaperHotkey { get; set; } = "Ctrl+F10";
    public int AutoReturnSeconds { get; set; }
    public string ReturnWallpaperHotkey { get; set; } = "Ctrl+F12";
    public bool AutoStartProgram { get; set; } = true;
    public int StartupDelaySeconds { get; set; } = 7;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PathToExe) || string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(PathToExe)))
            throw new ConfigException("PathToExe 必须包含有效的 EXE 文件名。");
        if (!Path.GetExtension(PathToExe).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ConfigException("PathToExe 必须指向 .exe 文件。");
        if (AutoReturnSeconds is < 0 or > 3600)
            throw new ConfigException("AutoReturnSeconds 必须在 0 到 3600 之间。");
        if (StartupDelaySeconds is < 0 or > 3600)
            throw new ConfigException("StartupDelaySeconds 必须在 0 到 3600 之间。");
        if (string.IsNullOrWhiteSpace(ExitWallpaperHotkey))
            throw new ConfigException("ExitWallpaperHotkey 不能为空。");
        if (string.IsNullOrWhiteSpace(ReturnWallpaperHotkey))
            throw new ConfigException("ReturnWallpaperHotkey 不能为空。");
    }
}

internal static class ConfigFile
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "config.json");
    private static readonly AppJsonContext JsonContext = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    public static AppConfig LoadOrCreate(out bool created)
    {
        created = !File.Exists(FilePath);
        if (created)
        {
            var config = new AppConfig();
            Save(config);
            Log.Info($"已创建默认配置文件: {FilePath}");
            return config;
        }

        var loaded = JsonSerializer.Deserialize(File.ReadAllText(FilePath), JsonContext.AppConfig)
                     ?? throw new ConfigException("配置文件内容为空。");
        Save(loaded);
        return loaded;
    }

    public static void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonContext.AppConfig);
        File.WriteAllText(FilePath, json);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
internal partial class AppJsonContext : JsonSerializerContext;

internal sealed class ConfigException(string message) : Exception(message);

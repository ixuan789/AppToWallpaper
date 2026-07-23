namespace AppToWallpaper;

internal readonly record struct Hotkey(int[] Modifiers, int Key)
{
    public static Hotkey Parse(string value)
    {
        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new ConfigException($"快捷键 '{value}' 格式错误，应类似 Ctrl+F10。");

        var modifiers = new int[parts.Length - 1];
        for (var i = 0; i < modifiers.Length; i++)
        {
            modifiers[i] = ParseModifier(parts[i]);
            if (Array.IndexOf(modifiers, modifiers[i], 0, i) >= 0)
                throw new ConfigException($"快捷键 '{value}' 包含重复修饰键。");
        }

        return new Hotkey(modifiers, ParseKey(parts[^1]));
    }

    public bool IsPressed()
    {
        foreach (var modifier in Modifiers)
        {
            if ((NativeMethods.GetAsyncKeyState(modifier) & 0x8000) == 0)
                return false;
        }
        return (NativeMethods.GetAsyncKeyState(Key) & 0x8000) != 0;
    }

    public bool Equals(Hotkey other)
    {
        if (Key != other.Key || Modifiers.Length != other.Modifiers.Length)
            return false;
        return Modifiers.Order().SequenceEqual(other.Modifiers.Order());
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        foreach (var modifier in Modifiers.Order())
            hash.Add(modifier);
        return hash.ToHashCode();
    }

    private static int ParseModifier(string value) => value.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => NativeMethods.VK_CONTROL,
        "ALT" => NativeMethods.VK_MENU,
        "SHIFT" => NativeMethods.VK_SHIFT,
        "WIN" or "WINDOWS" => NativeMethods.VK_LWIN,
        _ => throw new ConfigException($"未知快捷键修饰键: {value}")
    };

    private static int ParseKey(string value)
    {
        var upper = value.ToUpperInvariant();
        if (upper.Length == 1 && char.IsAsciiLetterOrDigit(upper[0]))
            return upper[0];
        if (upper.Length > 1 && upper[0] == 'F' && int.TryParse(upper.AsSpan(1), out var number) && number is >= 1 and <= 24)
            return NativeMethods.VK_F1 + number - 1;
        return upper switch
        {
            "ENTER" or "RETURN" => NativeMethods.VK_RETURN,
            "ESC" or "ESCAPE" => NativeMethods.VK_ESCAPE,
            "SPACE" => NativeMethods.VK_SPACE,
            "TAB" => NativeMethods.VK_TAB,
            "DELETE" or "DEL" => NativeMethods.VK_DELETE,
            _ => throw new ConfigException($"未知快捷键按键: {value}")
        };
    }
}

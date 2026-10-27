using System.Windows.Input;

namespace GameShell.Desktop.Shortcuts;

/// <summary>
/// Converts the user-facing hotkey format into Win32 registration values.
/// This class has no registration side effects and can be tested independently.
/// </summary>
public static class GlobalHotkeyParser
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    public static bool TryParse(string? value, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (var part in parts[..^1])
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModControl;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModAlt;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModShift;
            }
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModWin;
            }
        }

        if (modifiers == 0)
        {
            return false;
        }

        var keyName = parts[^1].ToUpperInvariant();
        var key = keyName.Length == 1 && (keyName[0] is >= 'A' and <= 'Z' || keyName[0] is >= '0' and <= '9')
            ? KeyInterop.KeyFromVirtualKey(keyName[0])
            : Enum.TryParse<Key>(keyName, true, out var parsedKey) ? parsedKey : Key.None;
        if (key == Key.None)
        {
            return false;
        }

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }
}

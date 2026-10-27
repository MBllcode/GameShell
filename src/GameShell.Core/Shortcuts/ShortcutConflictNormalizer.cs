using GameShell.Core.Settings;
using System.Globalization;
using System.Text;

namespace GameShell.Core.Shortcuts;

/// <summary>
/// Normalizes and removes duplicate profile shortcuts from persisted settings.
/// The first valid assignment wins, matching the existing compatibility behavior.
/// </summary>
public sealed class ShortcutConflictNormalizer
{
    public void Normalize(AppSettings settings)
    {
        EnsureUniqueProfileEnterHotkeys(settings.Display.GamingProfileConfigurations);
        EnsureUniqueProfileControllerShortcuts(settings.Display.GamingProfileConfigurations);
        EnsureGlobalWorkModeShortcutUniqueness(settings);
    }

    private static void EnsureUniqueProfileEnterHotkeys(IReadOnlyList<GamingProfileConfiguration> configurations)
    {
        var usedHotkeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuration in configurations)
        {
            var normalized = NormalizeHotkey(configuration.Hotkeys.EnterProfile);
            if (!string.IsNullOrEmpty(normalized) && !usedHotkeys.Add(normalized))
            {
                configuration.Hotkeys.EnterProfile = "";
            }
        }
    }

    private static void EnsureUniqueProfileControllerShortcuts(IReadOnlyList<GamingProfileConfiguration> configurations)
    {
        var usedShortcuts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuration in configurations)
        {
            var normalized = NormalizeControllerShortcut(configuration.ControllerShortcut);
            if (!string.IsNullOrEmpty(normalized) && !usedShortcuts.Add(normalized))
            {
                configuration.ControllerShortcut = null;
            }
        }
    }

    private static void EnsureGlobalWorkModeShortcutUniqueness(AppSettings settings)
    {
        var workModeHotkey = NormalizeHotkey(settings.Hotkeys.WorkMode);
        if (!string.IsNullOrEmpty(workModeHotkey))
        {
            foreach (var configuration in settings.Display.GamingProfileConfigurations)
            {
                if (NormalizeHotkey(configuration.Hotkeys.EnterProfile) == workModeHotkey)
                {
                    configuration.Hotkeys.EnterProfile = "";
                }
            }
        }

        var workModeControllerShortcut = NormalizeControllerShortcut(settings.Controller.WorkMode);
        if (string.IsNullOrEmpty(workModeControllerShortcut))
        {
            return;
        }

        foreach (var configuration in settings.Display.GamingProfileConfigurations)
        {
            if (NormalizeControllerShortcut(configuration.ControllerShortcut) == workModeControllerShortcut)
            {
                configuration.ControllerShortcut = null;
            }
        }
    }

    private static string NormalizeControllerShortcut(ControllerShortcutSettings? shortcut)
    {
        return shortcut is null || shortcut.Buttons.Count == 0
            ? string.Empty
            : string.Join('+', shortcut.Buttons.Select(button => button.ToString()).OrderBy(button => button, StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeHotkey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (parts.Count == 0)
        {
            return string.Empty;
        }

        var keyPart = RemoveDiacritics(parts[^1]).ToUpperInvariant();
        var modifierParts = parts
            .Take(parts.Count - 1)
            .Select(part => RemoveDiacritics(part).ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var normalizedModifiers = new List<string>();
        if (modifierParts.Contains("ctrl") || modifierParts.Contains("control")) normalizedModifiers.Add("Ctrl");
        if (modifierParts.Contains("alt")) normalizedModifiers.Add("Alt");
        if (modifierParts.Contains("shift")) normalizedModifiers.Add("Shift");
        if (modifierParts.Contains("win") || modifierParts.Contains("windows") || modifierParts.Contains("meta")) normalizedModifiers.Add("Win");

        if (normalizedModifiers.Count == 0 || string.IsNullOrWhiteSpace(keyPart))
        {
            return string.Empty;
        }

        normalizedModifiers.Add(keyPart);
        return string.Join('+', normalizedModifiers);
    }

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var filtered = new char[normalized.Length];
        var index = 0;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                filtered[index++] = character;
            }
        }

        return new string(filtered, 0, index).Normalize(NormalizationForm.FormC);
    }
}

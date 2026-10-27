namespace GameShell.Core.Runtime;

public static class TrayMenuText
{
    public static string OpenGameShell(string? language) => Translate(language, "Otwórz GameShell", "Open GameShell");

    public static string CurrentStatus(string? language, bool isWorkMode, string? activeProfileName)
    {
        if (isWorkMode)
        {
            return Translate(language, "Aktualnie: Konfiguracja domyślna", "Currently: Default setup");
        }

        if (!string.IsNullOrWhiteSpace(activeProfileName))
        {
            return Translate(language, $"Aktualnie: {activeProfileName}", $"Currently: {activeProfileName}");
        }

        return Translate(language, "Aktualnie: Nieznany tryb", "Currently: Unknown mode");
    }

    public static string GamingProfiles(string? language) => Translate(language, "Profile", "Profiles");

    public static string NoGamingProfiles(string? language) => Translate(language, "Brak profili", "No profiles");

    public static string ReturnToWorkMode(string? language) => Translate(language, "Przywróć konfigurację domyślną", "Restore default setup");


    public static string CloseGameShell(string? language) => Translate(language, "Zamknij GameShell", "Close GameShell");

    public static bool ShowRestorePreviousLayout(string? language) => false;

    private static string Translate(string? language, string polish, string english)
    {
        return string.Equals(language, "pl", StringComparison.OrdinalIgnoreCase) ? polish : english;
    }
}

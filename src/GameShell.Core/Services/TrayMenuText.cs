namespace GameShell.Core.Services;

public static class TrayMenuText
{
    public static string OpenGameShell(string? language) => Translate(language, "Otwórz GameShell", "Open GameShell");

    public static string CurrentStatus(string? language, bool isWorkMode, string? activeProfileName)
    {
        if (isWorkMode)
        {
            return Translate(language, "Aktualnie: Work Mode", "Currently: Work Mode");
        }

        if (!string.IsNullOrWhiteSpace(activeProfileName))
        {
            return Translate(language, $"Aktualnie: {activeProfileName}", $"Currently: {activeProfileName}");
        }

        return Translate(language, "Aktualnie: Nieznany tryb", "Currently: Unknown mode");
    }

    public static string GamingProfiles(string? language) => Translate(language, "Profile gamingowe", "Gaming profiles");

    public static string NoGamingProfiles(string? language) => Translate(language, "Brak profili gamingowych", "No gaming profiles");

    public static string ReturnToWorkMode(string? language) => Translate(language, "Wróć do Work Mode", "Return to Work Mode");

    public static string OpenWindowsDesktop(string? language) => Translate(language, "Otwórz pulpit Windows", "Open Windows Desktop");

    public static string CloseGameShell(string? language) => Translate(language, "Zamknij GameShell", "Close GameShell");

    public static bool ShowRestorePreviousLayout(string? language) => false;

    private static string Translate(string? language, string polish, string english)
    {
        return string.Equals(language, "pl", StringComparison.OrdinalIgnoreCase) ? polish : english;
    }
}

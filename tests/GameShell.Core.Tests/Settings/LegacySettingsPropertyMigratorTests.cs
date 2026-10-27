using GameShell.Core.Settings;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class LegacySettingsPropertyMigratorTests
{
    private readonly LegacySettingsPropertyMigrator _migrator = new();

    [Fact]
    public void Migrate_RenamesLegacyGamingPropertiesAcrossSections()
    {
        const string legacyJson = """
        {
          "pcGaming": { "completionDelaySeconds": 3 },
          "display": {
            "pcGamingLayout": { "displays": [] },
            "pcGamingProfiles": [],
            "activePcGamingProfileName": "TV",
            "pcGamingProfileConfigurations": [],
            "pcGamingProfileBehaviours": []
          },
          "hotkeys": { "pcGamingMode": "Ctrl+Alt+G" },
          "controller": { "pcGamingMode": "A" }
        }
        """;

        var migrated = _migrator.Migrate(legacyJson);

        Assert.Contains("\"gaming\"", migrated);
        Assert.Contains("\"gamingLayout\"", migrated);
        Assert.Contains("\"gamingProfiles\"", migrated);
        Assert.Contains("\"activeGamingProfileName\"", migrated);
        Assert.Contains("\"gamingProfileConfigurations\"", migrated);
        Assert.Contains("\"gamingProfileBehaviours\"", migrated);
        Assert.Contains("\"gamingMode\"", migrated);
        Assert.DoesNotContain("pcGaming", migrated, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migrate_PreservesCanonicalPropertyWhenBothNamesExist()
    {
        const string json = """
        {
          "gaming": { "completionDelaySeconds": 9 },
          "pcGaming": { "completionDelaySeconds": 2 }
        }
        """;

        var migrated = _migrator.Migrate(json);

        Assert.Contains("\"completionDelaySeconds\": 9", migrated);
        Assert.DoesNotContain("pcGaming", migrated, StringComparison.OrdinalIgnoreCase);
    }
}

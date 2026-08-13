using System.Text.Json.Nodes;
using System.Text.Json;

namespace GameShell.Core.Services;

/// <summary>
/// Migrates legacy persisted property names to the canonical settings contract.
/// It does not read or write files and never overrides a current property.
/// </summary>
public sealed class LegacySettingsPropertyMigrator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Migrate(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject;
        if (root is null)
        {
            return json;
        }

        Rename(root, "pcGaming", "gaming");

        if (root["display"] is JsonObject display)
        {
            Rename(display, "pcGamingLayout", "gamingLayout");
            Rename(display, "pcGamingProfiles", "gamingProfiles");
            Rename(display, "activePcGamingProfileName", "activeGamingProfileName");
            Rename(display, "pcGamingProfileConfigurations", "gamingProfileConfigurations");
            Rename(display, "pcGamingProfileBehaviours", "gamingProfileBehaviours");
        }

        if (root["hotkeys"] is JsonObject hotkeys)
        {
            Rename(hotkeys, "pcGamingMode", "gamingMode");
        }

        if (root["controller"] is JsonObject controller)
        {
            Rename(controller, "pcGamingMode", "gamingMode");
        }

        return root.ToJsonString(JsonOptions);
    }

    private static void Rename(JsonObject source, string legacyName, string currentName)
    {
        if (!source.TryGetPropertyValue(legacyName, out var value))
        {
            return;
        }

        if (!source.ContainsKey(currentName))
        {
            source[currentName] = value?.DeepClone();
        }

        source.Remove(legacyName);
    }
}

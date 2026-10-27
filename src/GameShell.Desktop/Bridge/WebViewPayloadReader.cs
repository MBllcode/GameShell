using GameShell.Core.Displays;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameShell.Desktop.Bridge;

internal static class WebViewPayloadReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ReadDependencyName(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object || !payload.Value.TryGetProperty("name", out var nameElement))
        {
            throw new InvalidOperationException("Dependency name is required.");
        }

        var dependencyName = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(dependencyName))
        {
            throw new InvalidOperationException("Dependency name is required.");
        }

        return dependencyName;
    }

    public static bool ReadHotkeyCaptureState(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object || !payload.Value.TryGetProperty("active", out var activeElement))
        {
            throw new InvalidOperationException("Hotkey capture state is required.");
        }

        return activeElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidOperationException("Hotkey capture state is invalid.")
        };
    }

    public static Guid ReadRequiredGuid(JsonElement? payload, string propertyName)
    {
        var value = ReadOptionalString(payload, propertyName);
        if (!Guid.TryParse(value, out var result)) throw new InvalidOperationException($"{propertyName} is required.");
        return result;
    }

    public static string? ReadOptionalString(JsonElement? payload, string propertyName)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object || !payload.Value.TryGetProperty(propertyName, out var property)) return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    public static DisplayIdentity ReadDisplayIdentity(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object || !payload.Value.TryGetProperty("identity", out var identityElement))
        {
            throw new InvalidOperationException("Display identity is required.");
        }

        return identityElement.Deserialize<DisplayIdentity>(JsonOptions)
            ?? throw new InvalidOperationException("Display identity is invalid.");
    }

    public static List<DisplayIdentity> ReadDisplayIdentities(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object || !payload.Value.TryGetProperty("identities", out var identitiesElement))
        {
            throw new InvalidOperationException("Display identities are required.");
        }

        var identities = identitiesElement.Deserialize<List<DisplayIdentity>>(JsonOptions);
        if (identities is null || identities.Count == 0)
        {
            throw new InvalidOperationException("Wybierz co najmniej jeden ekran.");
        }

        return identities;
    }

    public static string? ReadOptionalProfileName(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        if (!payload.Value.TryGetProperty("profileName", out var profileNameElement))
        {
            return null;
        }

        var profileName = profileNameElement.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(profileName) ? null : profileName;
    }

    public static string ReadRequiredProfileName(JsonElement? payload)
    {
        return ReadOptionalProfileName(payload)
            ?? throw new InvalidOperationException("Nazwa profilu jest wymagana.");
    }

    public static string ReadRequiredCaptureProfileId(JsonElement? payload)
    {
        var profileId = ReadOptionalString(payload, "profileId") ?? ReadOptionalProfileName(payload);
        return string.IsNullOrWhiteSpace(profileId)
            ? throw new InvalidOperationException("ProfileId is required.")
            : profileId.Trim();
    }
}

using GameShell.Core.Settings;

namespace GameShell.Core.Displays;

public static class GamingProfileTopologyValidator
{
    public static IReadOnlyList<string> Validate(GamingProfileDisplayTopology topology)
    {
        var errors = new List<string>();
        var displays = topology.Displays ?? [];

        for (var index = 0; index < displays.Count; index++)
        {
            if (displays.Skip(index + 1).Any(candidate =>
                    IsSameProfileDisplay(displays[index], candidate)))
            {
                errors.Add("Topology contains the same physical display more than once.");
                break;
            }
        }

        return errors;
    }

    private static bool IsSameProfileDisplay(ProfileDisplayTarget left, ProfileDisplayTarget right)
    {
        if (!string.IsNullOrWhiteSpace(left.KnownPhysicalDisplayId)
            && !string.IsNullOrWhiteSpace(right.KnownPhysicalDisplayId))
        {
            return string.Equals(left.KnownPhysicalDisplayId, right.KnownPhysicalDisplayId, StringComparison.OrdinalIgnoreCase);
        }

        return DisplayIdentityMatcher.IsSamePhysicalDisplay(left.Identity, right.Identity);
    }

    public static void EnsureValid(AppSettings settings)
    {
        foreach (var profile in settings.Display.GamingProfiles)
        {
            if (profile.DisplayTopology is null)
            {
                continue;
            }

            var errors = Validate(profile.DisplayTopology);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", errors));
            }
        }
    }
}

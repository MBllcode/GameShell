using GameShell.Core.Models;
using GameShell.Core.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class GamingProfileTopologyValidatorTests
{
    [Fact]
    public void Rejects_two_entries_for_the_same_adapter_target_even_when_names_differ()
    {
        var topology = new GamingProfileDisplayTopology
        {
            Displays =
            [
                Target("S90F", 10, 20, true),
                Target("SAMSUNG", 10, 20, false)
            ]
        };

        var errors = GamingProfileTopologyValidator.Validate(topology);

        Assert.Contains(errors, error => error.Contains("same physical display", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Allows_different_physical_targets_with_similar_names()
    {
        var topology = new GamingProfileDisplayTopology
        {
            Displays =
            [
                Target("SAMSUNG", 10, 20, true),
                Target("SAMSUNG", 10, 21, false)
            ]
        };

        Assert.Empty(GamingProfileTopologyValidator.Validate(topology));
    }

    private static ProfileDisplayTarget Target(string name, long adapterLuid, uint targetId, bool primary)
    {
        return new ProfileDisplayTarget
        {
            Identity = new DisplayIdentity
            {
                FriendlyName = name,
                AdapterLuid = adapterLuid,
                TargetId = targetId,
                MonitorDevicePath = $@"\\?\DISPLAY#{name}-{targetId}"
            },
            Enabled = true,
            IsPrimary = primary,
            Width = 3840,
            Height = 2160,
            RefreshPolicy = RefreshPolicy.AutoHighest
        };
    }
}

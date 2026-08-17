using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface IGamingProfileTopologyReadinessEvaluator
{
    Task<GamingProfileTopologyReadiness> EvaluateAsync(
        string profileId,
        string profileName,
        GamingProfileDisplayTopology topology,
        CancellationToken cancellationToken = default,
        bool deferInactiveModeValidation = false,
        DisplaySettings? displaySettings = null);
}

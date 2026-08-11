using GameShell.Core.Models;

namespace GameShell.Core.Services;

public enum DisplayIdentityMatchConfidence
{
    None,
    WeakFriendlyName,
    EdidProduct,
    EdidProductAndConnector,
    ExactMonitorDevicePath
}

public static class DisplayIdentityMatcher
{
    public static DisplayIdentityMatchConfidence GetMatchConfidence(DisplayIdentity saved, DisplayIdentity detected)
    {
        if (!string.IsNullOrWhiteSpace(saved.MonitorDevicePath)
            && !string.IsNullOrWhiteSpace(detected.MonitorDevicePath)
            && string.Equals(saved.MonitorDevicePath, detected.MonitorDevicePath, StringComparison.OrdinalIgnoreCase))
        {
            return DisplayIdentityMatchConfidence.ExactMonitorDevicePath;
        }

        var hasSavedEdid = saved.EdidManufacturerId != 0 && saved.EdidProductCodeId != 0;
        var hasDetectedEdid = detected.EdidManufacturerId != 0 && detected.EdidProductCodeId != 0;
        if (hasSavedEdid
            && hasDetectedEdid
            && saved.EdidManufacturerId == detected.EdidManufacturerId
            && saved.EdidProductCodeId == detected.EdidProductCodeId)
        {
            return saved.ConnectorInstance != 0
                && detected.ConnectorInstance != 0
                && saved.ConnectorInstance == detected.ConnectorInstance
                    ? DisplayIdentityMatchConfidence.EdidProductAndConnector
                    : DisplayIdentityMatchConfidence.EdidProduct;
        }

        if (!string.IsNullOrWhiteSpace(saved.FriendlyName)
            && !string.IsNullOrWhiteSpace(detected.FriendlyName)
            && string.Equals(saved.FriendlyName, detected.FriendlyName, StringComparison.OrdinalIgnoreCase))
        {
            return DisplayIdentityMatchConfidence.WeakFriendlyName;
        }

        return DisplayIdentityMatchConfidence.None;
    }

    public static bool IsReliableMatch(DisplayIdentity saved, DisplayIdentity detected)
    {
        return GetMatchConfidence(saved, detected) >= DisplayIdentityMatchConfidence.EdidProduct;
    }

    public static int GetStableIdentityScore(DisplayIdentity saved, DisplayIdentity detected)
    {
        var score = 0;
        if (saved.AdapterLuid != 0 && saved.AdapterLuid == detected.AdapterLuid)
        {
            score += 40;
        }

        if (saved.TargetId != 0 && saved.TargetId == detected.TargetId)
        {
            score += saved.AdapterLuid != 0 && saved.AdapterLuid == detected.AdapterLuid ? 45 : 10;
        }

        if (saved.ConnectorInstance != 0 && saved.ConnectorInstance == detected.ConnectorInstance)
        {
            score += 25;
        }

        if (!string.IsNullOrWhiteSpace(saved.OutputTechnology)
            && !string.IsNullOrWhiteSpace(detected.OutputTechnology)
            && string.Equals(saved.OutputTechnology, detected.OutputTechnology, StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }

        if (saved.EdidManufacturerId != 0
            && saved.EdidProductCodeId != 0
            && saved.EdidManufacturerId == detected.EdidManufacturerId
            && saved.EdidProductCodeId == detected.EdidProductCodeId)
        {
            score += 35;
        }

        if (!string.IsNullOrWhiteSpace(saved.MonitorDevicePath)
            && !string.IsNullOrWhiteSpace(detected.MonitorDevicePath)
            && string.Equals(saved.MonitorDevicePath, detected.MonitorDevicePath, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        return score;
    }
}

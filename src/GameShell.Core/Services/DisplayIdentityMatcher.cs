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
}

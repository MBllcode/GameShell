using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace GameShell.Windows.Displays;

public sealed class WindowsDisplayRecoveryService : IDisplayRecoveryService
{
    private static readonly TimeSpan RecoveryWait = TimeSpan.FromMilliseconds(1500);
    private readonly ILogger<WindowsDisplayRecoveryService> _logger;

    public WindowsDisplayRecoveryService(ILogger<WindowsDisplayRecoveryService> logger)
    {
        _logger = logger;
    }

    public async Task<DisplayRecoveryAttempt> RecoverAsync(
        string profileName,
        ProfileDisplayTarget savedTarget,
        DisplayDeviceInfo resolvedTarget,
        IReadOnlyList<DisplayModeInfo> beforeModes,
        CancellationToken cancellationToken = default)
    {
        var devicePath = resolvedTarget.Identity.MonitorDevicePath;
        var devNode = ResolveDevNode(devicePath);
        _logger.LogInformation(
            "[DISPLAY RECOVERY TARGET] SavedName={SavedName} ResolvedName={ResolvedName} MonitorDevicePath={MonitorDevicePath} DevNodeInstanceId={DevNodeInstanceId} AdapterLuid={AdapterLuid} TargetId={TargetId} ConnectorInstance={ConnectorInstance}",
            savedTarget.Identity.FriendlyName,
            resolvedTarget.Name,
            devicePath,
            devNode ?? "-",
            resolvedTarget.AdapterLuid,
            resolvedTarget.TargetId,
            resolvedTarget.Identity.ConnectorInstance);

        var attempt = new DisplayRecoveryAttempt
        {
            ProfileName = profileName,
            SavedTarget = savedTarget,
            ResolvedTarget = resolvedTarget,
            Mechanism = "CM_Locate_DevNode + CM_Reenumerate_DevNode",
            DevNodeInstanceId = devNode ?? string.Empty,
            BeforeModes = beforeModes,
            Result = DisplayRecoveryResult.TargetNotResolved
        };

        if (string.IsNullOrWhiteSpace(devNode))
        {
            return attempt;
        }

        var locateResult = DisplayConfigNative.CM_Locate_DevNode(
            out var devInst,
            devNode,
            DisplayConfigNative.CmLocateDevnodeNormal);
        if (locateResult == DisplayConfigNative.CrAccessDenied)
        {
            return attempt with
            {
                LocateDevNodeResultCode = locateResult,
                Result = DisplayRecoveryResult.AccessDenied
            };
        }

        if (locateResult != DisplayConfigNative.CrSuccess)
        {
            _logger.LogWarning("[DISPLAY RECOVERY] CM_Locate_DevNode failed. DevNode={DevNode} ConfigRet={ConfigRet}.", devNode, locateResult);
            return attempt with
            {
                LocateDevNodeResultCode = locateResult,
                Result = DisplayRecoveryResult.Failed
            };
        }

        var reenumerateResult = DisplayConfigNative.CM_Reenumerate_DevNode(devInst, DisplayConfigNative.CmReenumerateSynch);
        if (reenumerateResult == DisplayConfigNative.CrAccessDenied)
        {
            return attempt with
            {
                LocateDevNodeResultCode = locateResult,
                ReenumerateDevNodeResultCode = reenumerateResult,
                Result = DisplayRecoveryResult.AccessDenied
            };
        }

        if (reenumerateResult != DisplayConfigNative.CrSuccess)
        {
            _logger.LogWarning("[DISPLAY RECOVERY] CM_Reenumerate_DevNode failed. DevNode={DevNode} ConfigRet={ConfigRet}.", devNode, reenumerateResult);
            return attempt with
            {
                LocateDevNodeResultCode = locateResult,
                ReenumerateDevNodeResultCode = reenumerateResult,
                Result = DisplayRecoveryResult.Failed
            };
        }

        await Task.Delay(RecoveryWait, cancellationToken);
        return attempt with
        {
            LocateDevNodeResultCode = locateResult,
            ReenumerateDevNodeResultCode = reenumerateResult,
            WaitMs = (long)RecoveryWait.TotalMilliseconds,
            Result = DisplayRecoveryResult.Succeeded
        };
    }

    private static string? ResolveDevNode(string monitorDevicePath)
    {
        if (string.IsNullOrWhiteSpace(monitorDevicePath))
        {
            return null;
        }

        var classGuid = DisplayConfigNative.GuidDevinterfaceMonitor;
        var deviceInfoSet = DisplayConfigNative.SetupDiGetClassDevs(
            ref classGuid,
            null,
            IntPtr.Zero,
            DisplayConfigNative.DigcfPresent | DisplayConfigNative.DigcfDeviceInterface);
        if (deviceInfoSet == new IntPtr(-1))
        {
            return null;
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = Marshal.AllocHGlobal(IntPtr.Size == 8 ? 32 : 28);
                try
                {
                    Marshal.WriteInt32(interfaceData, 0, IntPtr.Size == 8 ? 32 : 28);
                    Marshal.WriteInt32(interfaceData, 4, 0);
                    Marshal.WriteInt32(interfaceData, 8, 0);
                    Marshal.WriteInt32(interfaceData, 12, 0);
                    Marshal.WriteInt32(interfaceData, 16, 0);
                    Marshal.WriteInt32(interfaceData, 20, 0);
                    Marshal.WriteInt32(interfaceData, 24, 0);
                    Marshal.WriteInt32(interfaceData, 28, 0);
                    if (!DisplayConfigNative.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref classGuid, index, interfaceData))
                    {
                        break;
                    }

                    DisplayConfigNative.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet,
                        interfaceData,
                        IntPtr.Zero,
                        0,
                        out var requiredSize,
                        IntPtr.Zero);
                    if (requiredSize == 0)
                    {
                        continue;
                    }

                    var detailData = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detailData, 0, IntPtr.Size == 8 ? 8 : 6);
                        var deviceInfo = SetupDeviceInfoData.Create();
                        if (!DisplayConfigNative.SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet,
                            interfaceData,
                            detailData,
                            requiredSize,
                            out _,
                            ref deviceInfo))
                        {
                            continue;
                        }

                        var path = Marshal.PtrToStringUni(detailData + 4);
                        if (!string.Equals(path, monitorDevicePath, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var instanceId = new char[512];
                        if (!DisplayConfigNative.SetupDiGetDeviceInstanceId(deviceInfoSet, ref deviceInfo, instanceId, (uint)instanceId.Length, out var length))
                        {
                            return null;
                        }

                        return new string(instanceId, 0, checked((int)Math.Max(0, length - 1)));
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailData);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(interfaceData);
                }
            }
        }
        finally
        {
            DisplayConfigNative.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return null;
    }
}
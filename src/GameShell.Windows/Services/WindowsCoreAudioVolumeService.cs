using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Services;

internal enum VolumeButtonAction
{
    VolumeUp,
    VolumeDown,
    Mute
}

internal readonly record struct AudioEndpointState(float Volume, bool IsMuted);

/// <summary>
/// Minimal Core Audio adapter used only by the Console Shell hardware-button
/// compatibility path. It deliberately does not own audio-device selection.
/// </summary>
public sealed class WindowsCoreAudioVolumeService
{
    private static readonly Guid MmDeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid AudioEndpointVolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private readonly ILogger<WindowsCoreAudioVolumeService> _logger;

    public WindowsCoreAudioVolumeService(ILogger<WindowsCoreAudioVolumeService> logger)
    {
        _logger = logger;
    }

    internal bool TryGetState(out AudioEndpointState state)
    {
        state = default;
        if (!TryGetEndpointVolume(out var device, out var endpointVolume))
        {
            return false;
        }

        try
        {
            var volumeHr = endpointVolume.GetMasterVolumeLevelScalar(out var volume);
            var muteHr = endpointVolume.GetMute(out var isMuted);
            if (volumeHr < 0 || muteHr < 0)
            {
                return false;
            }

            state = new AudioEndpointState(Math.Clamp(volume, 0f, 1f), isMuted);
            return true;
        }
        catch (COMException exception)
        {
            _logger.LogDebug(exception, "Core Audio endpoint state could not be read.");
            return false;
        }
        finally
        {
            ReleaseComObject(endpointVolume);
            ReleaseComObject(device);
        }
    }

    internal bool TryApply(VolumeButtonAction action)
    {
        if (!TryGetEndpointVolume(out var device, out var endpointVolume))
        {
            return false;
        }

        try
        {
            var eventContext = Guid.Empty;
            int hr;
            switch (action)
            {
                case VolumeButtonAction.VolumeUp:
                    hr = endpointVolume.GetMasterVolumeLevelScalar(out var upVolume);
                    if (hr < 0)
                    {
                        return false;
                    }

                    hr = endpointVolume.SetMasterVolumeLevelScalar(
                        Math.Clamp(upVolume + 0.05f, 0f, 1f),
                        ref eventContext);
                    break;

                case VolumeButtonAction.VolumeDown:
                    hr = endpointVolume.GetMasterVolumeLevelScalar(out var downVolume);
                    if (hr < 0)
                    {
                        return false;
                    }

                    hr = endpointVolume.SetMasterVolumeLevelScalar(
                        Math.Clamp(downVolume - 0.05f, 0f, 1f),
                        ref eventContext);
                    break;

                case VolumeButtonAction.Mute:
                    hr = endpointVolume.GetMute(out var isMuted);
                    if (hr < 0)
                    {
                        return false;
                    }

                    hr = endpointVolume.SetMute(!isMuted, ref eventContext);
                    break;

                default:
                    return false;
            }

            return hr >= 0;
        }
        catch (COMException exception)
        {
            _logger.LogDebug(exception, "Core Audio volume action failed. Action={Action}", action);
            return false;
        }
        finally
        {
            ReleaseComObject(endpointVolume);
            ReleaseComObject(device);
        }
    }

    private static bool TryGetEndpointVolume(out IMMDevice device, out IAudioEndpointVolume endpointVolume)
    {
        device = null!;
        endpointVolume = null!;
        IMMDeviceEnumerator? enumerator = null;

        try
        {
            var enumeratorType = Type.GetTypeFromCLSID(MmDeviceEnumeratorClsid, throwOnError: true)
                ?? throw new InvalidOperationException("MMDeviceEnumerator is unavailable.");
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;
            var hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
            if (hr < 0)
            {
                return false;
            }

            var interfaceId = AudioEndpointVolumeIid;
            hr = device.Activate(ref interfaceId, ClsctxInprocServer, IntPtr.Zero, out endpointVolume);
            if (hr < 0)
            {
                ReleaseComObject(device);
                return false;
            }

            return true;
        }
        catch (COMException)
        {
            ReleaseComObject(endpointVolume);
            ReleaseComObject(device);
            return false;
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private const uint ClsctxInprocServer = 0x1;

    private enum EDataFlow
    {
        Render,
        Capture,
        All
    }

    private enum ERole
    {
        Console,
        Multimedia,
        Communications
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice endpoint);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid interfaceId,
            uint classContext,
            IntPtr activationParameters,
            [MarshalAs(UnmanagedType.Interface)] out IAudioEndpointVolume endpointVolume);

        [PreserveSig]
        int OpenPropertyStore(uint access, out IntPtr properties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out uint state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int GetChannelCount(out uint channelCount);

        [PreserveSig]
        int SetMasterVolumeLevel(float level, ref Guid eventContext);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolumeLevel(out float level);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);

        [PreserveSig]
        int GetVolumeStepInfo(out uint step, out uint stepCount);

        [PreserveSig]
        int VolumeStepUp(ref Guid eventContext);

        [PreserveSig]
        int VolumeStepDown(ref Guid eventContext);

        [PreserveSig]
        int QueryHardwareSupport(out uint hardwareSupportMask);

        [PreserveSig]
        int GetHardwareSupportMask(out uint hardwareSupportMask);
    }
}

using System.Runtime.InteropServices;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameShell.Windows.Services;

public sealed class WindowsAudioDeviceService : IAudioDeviceService
{
    private const int DeviceStateActive = 0x00000001;
    private const int DeviceStateDisabled = 0x00000002;
    private const int DeviceStateNotPresent = 0x00000004;
    private const int DeviceStateUnplugged = 0x00000008;
    private const int DeviceStateAll = DeviceStateActive | DeviceStateDisabled | DeviceStateNotPresent | DeviceStateUnplugged;
    private static readonly Guid MMDeviceEnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid PolicyConfigClientClassId = new("870af99c-171d-4f9e-af0d-e63df40c2bc9");
    private const string RenderDevicesRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";
    private const string DeviceStateRegistryValue = "DeviceState";
    private static readonly PropertyKey DeviceFriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0b"), 14);
    private readonly ILogger<WindowsAudioDeviceService> _logger;

    public WindowsAudioDeviceService(ILogger<WindowsAudioDeviceService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<AudioDeviceSnapshot>> GetRenderDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<AudioDeviceSnapshot>>(ReadRenderDevices());
    }

    public Task<AudioDeviceSnapshot?> GetDefaultRenderDeviceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var enumerator = ComScope.Create(CreateComInstance<IMMDeviceEnumerator>(MMDeviceEnumeratorClassId));
            enumerator.Instance.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device);
            using var scope = ComScope.Create(device);
            var snapshot = new AudioDeviceSnapshot
            {
                Id = GetDeviceId(scope.Instance),
                Name = GetDeviceName(scope.Instance),
                State = "Active",
                IsActive = true
            };

            return Task.FromResult<AudioDeviceSnapshot?>(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Default audio render device could not be read.");
            return Task.FromResult<AudioDeviceSnapshot?>(null);
        }
    }

    public Task<AudioOperationResult> SetBestRenderDeviceForDisplayAsync(
        DisplayIdentity display,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var devices = ReadRenderDevices()
            .OrderByDescending(device => device.IsActive)
            .ToList();
        var match = SelectBestDevice(display, devices);
        if (match is null)
        {
            _logger.LogWarning(
                "No active audio endpoint matched display {DisplayName}. Active endpoints: {EndpointNames}.",
                display.FriendlyName,
                string.Join(", ", devices.Select(device => device.Name)));
            return Task.FromResult(AudioOperationResult.Fail("Nie znaleziono urządzenia audio pasującego do wybranego ekranu."));
        }

        var result = SetDefaultRenderDeviceWithFallback(match.Id, match.Name, cancellationToken);
        if (result.Success)
        {
            _logger.LogInformation(
                "Default audio endpoint switched for display {DisplayName}. Endpoint: {EndpointName}.",
                display.FriendlyName,
                match.Name);
            return Task.FromResult(AudioOperationResult.Ok($"Przelaczono audio na {match.Name}."));
        }

        return Task.FromResult(result);
    }

    private IReadOnlyList<AudioDeviceSnapshot> ReadRenderDevices()
    {
        var coreAudioDevices = TryReadDevices(() => EnumerateRenderDevices(DeviceStateAll), "CoreAudio render endpoints");
        var registryDevices = TryReadDevices(EnumerateRegistryRenderDevices, "registry render endpoints");
        var devices = MergeAudioDevices(coreAudioDevices, registryDevices);
        _logger.LogInformation(
            "Audio render devices read. CoreAudio: {CoreAudioCount}, Registry: {RegistryCount}, Merged: {MergedCount}.",
            coreAudioDevices.Count,
            registryDevices.Count,
            devices.Count);
        return devices;
    }

    private IReadOnlyList<AudioDeviceSnapshot> TryReadDevices(
        Func<IReadOnlyList<AudioDeviceSnapshot>> read,
        string source)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio render devices could not be read from {Source}.", source);
            return [];
        }
    }

    public Task<AudioOperationResult> SetDefaultRenderDeviceAsync(
        string deviceId,
        string? deviceName = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SetDefaultRenderDeviceWithFallback(deviceId, deviceName, cancellationToken));
    }

    private AudioOperationResult SetDefaultRenderDevice(string deviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return AudioOperationResult.Fail("Brak identyfikatora urządzenia audio.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var policyConfig = ComScope.Create(CreateComInstance<IPolicyConfig>(PolicyConfigClientClassId));
            policyConfig.Instance.SetDefaultEndpoint(deviceId, ERole.Console);
            policyConfig.Instance.SetDefaultEndpoint(deviceId, ERole.Multimedia);
            policyConfig.Instance.SetDefaultEndpoint(deviceId, ERole.Communications);
            return AudioOperationResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Default audio endpoint could not be changed. DeviceId: {DeviceId}", deviceId);
            return AudioOperationResult.Fail("Nie udało się przełączyć domyślnego urządzenia audio.");
        }
    }

    private AudioOperationResult SetDefaultRenderDeviceWithFallback(string deviceId, string? deviceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return AudioOperationResult.Fail("Brak identyfikatora urzÄ…dzenia audio.");
        }

        var candidates = GetEquivalentDeviceCandidates(deviceId, deviceName);
        var lastFailure = "Nie udaÅ‚o siÄ™ przeÅ‚Ä…czyÄ‡ domyÅ›lnego urzÄ…dzenia audio.";
        foreach (var candidate in candidates)
        {
            var result = SetDefaultRenderDevice(candidate.Id, cancellationToken);
            if (!result.Success)
            {
                lastFailure = result.Message ?? lastFailure;
                continue;
            }

            var current = GetDefaultRenderDeviceAsync(cancellationToken).GetAwaiter().GetResult();
            if (current is not null && IsEquivalentAudioDevice(current, candidate))
            {
                return AudioOperationResult.Ok();
            }

            _logger.LogWarning(
                "Default audio endpoint call succeeded but verification did not match. Requested: {RequestedEndpoint}, Actual: {ActualEndpoint}.",
                candidate.Name,
                current?.Name ?? "unknown");
        }

        return AudioOperationResult.Fail(lastFailure);
    }

    private IReadOnlyList<AudioDeviceSnapshot> GetEquivalentDeviceCandidates(string deviceId, string? deviceName)
    {
        var devices = ReadRenderDevices();
        var requested = devices.FirstOrDefault(device => device.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
        var requestedName = !string.IsNullOrWhiteSpace(deviceName)
            ? deviceName
            : requested?.Name;
        var requestedKey = NormalizeAudioDeviceProfileKey(requestedName);
        var candidates = new List<AudioDeviceSnapshot>();

        if (requested is not null)
        {
            candidates.Add(requested);
        }
        else
        {
            candidates.Add(new AudioDeviceSnapshot
            {
                Id = deviceId,
                Name = requestedName ?? deviceId,
                State = "Unknown"
            });
        }

        if (!string.IsNullOrWhiteSpace(requestedKey))
        {
            candidates.AddRange(devices.Where(device => NormalizeAudioDeviceProfileKey(device.Name).Equals(requestedKey, StringComparison.OrdinalIgnoreCase)));
        }

        return candidates
            .Where(device => !string.IsNullOrWhiteSpace(device.Id))
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(GetAudioDeviceStateRank).First())
            .OrderByDescending(GetAudioDeviceStateRank)
            .ToList();
    }

    private static bool IsEquivalentAudioDevice(AudioDeviceSnapshot actual, AudioDeviceSnapshot requested)
    {
        if (actual.Id.Equals(requested.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var actualKey = NormalizeAudioDeviceProfileKey(actual.Name);
        var requestedKey = NormalizeAudioDeviceProfileKey(requested.Name);
        return !string.IsNullOrWhiteSpace(actualKey)
            && actualKey.Equals(requestedKey, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<AudioDeviceSnapshot> EnumerateRenderDevices(int stateMask)
    {
        var devices = new List<AudioDeviceSnapshot>();
        using var enumerator = ComScope.Create(CreateComInstance<IMMDeviceEnumerator>(MMDeviceEnumeratorClassId));
        enumerator.Instance.EnumAudioEndpoints(EDataFlow.Render, stateMask, out var collection);
        using var collectionScope = ComScope.Create(collection);
        collectionScope.Instance.GetCount(out var count);

        for (uint index = 0; index < count; index++)
        {
            try
            {
                collectionScope.Instance.Item(index, out var device);
                using var deviceScope = ComScope.Create(device);
                deviceScope.Instance.GetState(out var state);
                devices.Add(new AudioDeviceSnapshot
                {
                    Id = GetDeviceId(deviceScope.Instance),
                    Name = GetDeviceName(deviceScope.Instance),
                    State = FormatDeviceState(state),
                    IsActive = (state & DeviceStateActive) == DeviceStateActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Audio render endpoint could not be read. Index: {Index}", index);
            }
        }

        return devices
            .OrderByDescending(device => device.IsActive)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<AudioDeviceSnapshot> EnumerateRegistryRenderDevices()
    {
        var devices = new List<AudioDeviceSnapshot>();
        using var renderKey = Registry.LocalMachine.OpenSubKey(RenderDevicesRegistryPath);
        if (renderKey is null)
        {
            _logger.LogDebug("Audio render device registry path is not available.");
            return devices;
        }

        foreach (var deviceKeyName in renderKey.GetSubKeyNames())
        {
            try
            {
                using var deviceKey = renderKey.OpenSubKey(deviceKeyName);
                using var propertiesKey = deviceKey?.OpenSubKey("Properties");
                if (deviceKey is null)
                {
                    continue;
                }

                var state = ReadRegistryDeviceState(deviceKey);
                var name = ReadBestRegistryDeviceName(propertiesKey) ?? deviceKeyName;
                devices.Add(new AudioDeviceSnapshot
                {
                    Id = ToEndpointId(deviceKeyName),
                    Name = name,
                    State = FormatDeviceState(state),
                    IsActive = (state & DeviceStateActive) == DeviceStateActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Registry audio render endpoint could not be read. Key: {DeviceKeyName}", deviceKeyName);
            }
        }

        return devices;
    }

    private static IReadOnlyList<AudioDeviceSnapshot> MergeAudioDevices(params IReadOnlyList<AudioDeviceSnapshot>[] sources)
    {
        var devicesById = sources
            .SelectMany(source => source)
            .Where(device => !string.IsNullOrWhiteSpace(device.Id))
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var best = group.OrderByDescending(GetAudioDeviceStateRank).First();
                return string.IsNullOrWhiteSpace(best.Name)
                    ? best with { Name = group.Select(device => device.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? best.Id }
                    : best;
            })
            .ToList();

        return devicesById
            .GroupBy(device => NormalizeAudioDeviceProfileKey(device.Name), StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
            {
                if (string.IsNullOrWhiteSpace(group.Key))
                {
                    return group;
                }

                return [group.OrderByDescending(GetAudioDeviceStateRank).First()];
            })
            .OrderByDescending(device => device.IsActive)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int GetAudioDeviceStateRank(AudioDeviceSnapshot device)
    {
        if (device.IsActive || device.State.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (device.State.Equals("Unplugged", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (device.State.Equals("NotPresent", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (device.State.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private static string NormalizeAudioDeviceName(string value)
    {
        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeAudioDeviceProfileKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = NormalizeAudioDeviceName(value).ToUpperInvariant();
        if (normalized.Contains("S90F", StringComparison.Ordinal))
        {
            return "S90F";
        }

        if (normalized.Contains("SAMSUNG", StringComparison.Ordinal))
        {
            return "SAMSUNG";
        }

        if (normalized.Contains("MI MONITOR", StringComparison.Ordinal))
        {
            return "MI MONITOR";
        }

        if (normalized.Contains("NVIDIA OUTPUT", StringComparison.Ordinal))
        {
            return "NVIDIA OUTPUT";
        }

        if (normalized.Contains("DIGITAL AUDIO (HDMI)", StringComparison.Ordinal))
        {
            return normalized;
        }

        return normalized;
    }

    private static int ReadRegistryDeviceState(RegistryKey deviceKey)
    {
        return deviceKey.GetValue(DeviceStateRegistryValue) is int state
            ? state
            : DeviceStateNotPresent;
    }

    private static string? ReadBestRegistryDeviceName(RegistryKey? key)
    {
        if (key is null)
        {
            return null;
        }

        var candidates = key.GetValueNames()
            .Select(name => new
            {
                ValueName = name,
                Value = key.GetValue(name) as string
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => new
            {
                item.ValueName,
                Value = item.Value!.Trim()
            })
            .Where(item => IsUsableRegistryDeviceName(item.Value))
            .OrderBy(item => ScoreRegistryDeviceName(item.ValueName, item.Value))
            .ToList();

        return candidates.FirstOrDefault()?.Value;
    }

    private static bool IsUsableRegistryDeviceName(string value)
    {
        return !value.StartsWith("{", StringComparison.Ordinal)
            && !value.StartsWith("%", StringComparison.Ordinal)
            && !value.Contains('\\', StringComparison.Ordinal)
            && !value.Contains("HDAUDIO", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("SWD\\MMDEVAPI", StringComparison.OrdinalIgnoreCase)
            && !value.Contains(".inf:", StringComparison.OrdinalIgnoreCase);
    }

    private static int ScoreRegistryDeviceName(string valueName, string value)
    {
        if (valueName.EndsWith(",14", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (valueName.EndsWith(",2", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (value.Contains("HDMI", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Display", StringComparison.OrdinalIgnoreCase)
            || value.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
            || value.Contains("AMD", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Samsung", StringComparison.OrdinalIgnoreCase)
            || value.Contains("S90", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 10;
    }

    private static string ToEndpointId(string registryKeyName)
    {
        return registryKeyName.StartsWith("{0.0.", StringComparison.OrdinalIgnoreCase)
            ? registryKeyName
            : $"{{0.0.0.00000000}}.{registryKeyName}";
    }

    private static string FormatDeviceState(int state)
    {
        if ((state & DeviceStateActive) == DeviceStateActive)
        {
            return "Active";
        }

        if ((state & DeviceStateUnplugged) == DeviceStateUnplugged)
        {
            return "Unplugged";
        }

        if ((state & DeviceStateNotPresent) == DeviceStateNotPresent)
        {
            return "NotPresent";
        }

        if ((state & DeviceStateDisabled) == DeviceStateDisabled)
        {
            return "Disabled";
        }

        return "Unknown";
    }

    private static AudioDeviceSnapshot? SelectBestDevice(DisplayIdentity display, IReadOnlyList<AudioDeviceSnapshot> devices)
    {
        var displayTokens = Tokenize(display.FriendlyName)
            .Concat(Tokenize(display.MonitorDevicePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (displayTokens.Count == 0)
        {
            return null;
        }

        var ranked = devices
            .Select(device => new
            {
                Device = device,
                Score = ScoreDevice(device, displayTokens)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Device.IsActive)
            .ToList();

        return ranked.Count == 0 ? null : ranked[0].Device;
    }

    private static int ScoreDevice(AudioDeviceSnapshot device, IReadOnlyList<string> displayTokens)
    {
        var deviceName = device.Name.ToUpperInvariant();
        var score = 0;
        foreach (var token in displayTokens)
        {
            if (deviceName.Contains(token.ToUpperInvariant(), StringComparison.Ordinal))
            {
                score += token.Length >= 6 ? 4 : 2;
            }
        }

        if (deviceName.Contains("HDMI", StringComparison.OrdinalIgnoreCase)
            || deviceName.Contains("DISPLAY", StringComparison.OrdinalIgnoreCase)
            || deviceName.Contains("NVIDIA HIGH DEFINITION AUDIO", StringComparison.OrdinalIgnoreCase)
            || deviceName.Contains("AMD HIGH DEFINITION AUDIO", StringComparison.OrdinalIgnoreCase))
        {
            score++;
        }

        return score;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var current = new List<char>();
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Add(character);
                continue;
            }

            foreach (var token in FlushToken(current))
            {
                yield return token;
            }
        }

        foreach (var token in FlushToken(current))
        {
            yield return token;
        }
    }

    private static IEnumerable<string> FlushToken(List<char> current)
    {
        if (current.Count == 0)
        {
            yield break;
        }

        var token = new string(current.ToArray());
        current.Clear();
        if (token.Length >= 3 && !IsGenericToken(token))
        {
            yield return token;
        }
    }

    private static bool IsGenericToken(string token)
    {
        return token.Equals("DISPLAY", StringComparison.OrdinalIgnoreCase)
            || token.Equals("MONITOR", StringComparison.OrdinalIgnoreCase)
            || token.Equals("DEVICE", StringComparison.OrdinalIgnoreCase)
            || token.Equals("AUDIO", StringComparison.OrdinalIgnoreCase)
            || token.Equals("OLED", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDeviceId(IMMDevice device)
    {
        device.GetId(out var pointer);
        try
        {
            return Marshal.PtrToStringUni(pointer) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    private static T CreateComInstance<T>(Guid classId)
        where T : class
    {
        var type = Type.GetTypeFromCLSID(classId)
            ?? throw new InvalidOperationException($"COM class is not available: {classId}.");
        return (T)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"COM class could not be created: {classId}."));
    }

    private static string GetDeviceName(IMMDevice device)
    {
        device.OpenPropertyStore(StorageAccessMode.Read, out var propertyStore);
        using var scope = ComScope.Create(propertyStore);
        var key = DeviceFriendlyName;
        scope.Instance.GetValue(ref key, out var value);
        try
        {
            return value.ValueType == VarEnum.VT_LPWSTR
                ? Marshal.PtrToStringUni(value.PointerValue) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    [DllImport("Ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant propVariant);

    private sealed class ComScope<T> : IDisposable
        where T : class
    {
        private ComScope(T instance)
        {
            Instance = instance;
        }

        public T Instance { get; }

        public static ComScope<T> Create(T instance)
        {
            return new ComScope<T>(instance);
        }

        public void Dispose()
        {
            if (Marshal.IsComObject(Instance))
            {
                Marshal.ReleaseComObject(Instance);
            }
        }
    }

    private static class ComScope
    {
        public static ComScope<T> Create<T>(T instance)
            where T : class
        {
            return ComScope<T>.Create(instance);
        }
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-C0F45A41C2DE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        void GetCount(out uint count);
        void Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid interfaceId, int classContext, IntPtr activationParams, out IntPtr interfacePointer);
        void OpenPropertyStore(StorageAccessMode accessMode, out IPropertyStore propertyStore);
        void GetId(out IntPtr id);
        void GetState(out int state);
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
    }

    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        void GetMixFormat();
        void GetDeviceFormat();
        void ResetDeviceFormat();
        void SetDeviceFormat();
        void GetProcessingPeriod();
        void SetProcessingPeriod();
        void GetShareMode();
        void SetShareMode();
        void GetPropertyValue();
        void SetPropertyValue();
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
        void SetEndpointVisibility();
    }

    private readonly record struct PropertyKey(Guid FormatId, int PropertyId);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public VarEnum ValueType;
        private readonly ushort _reserved1;
        private readonly ushort _reserved2;
        private readonly ushort _reserved3;
        public IntPtr PointerValue;
        private readonly int _reserved4;
    }

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

    private enum StorageAccessMode
    {
        Read,
        Write,
        ReadWrite
    }
}

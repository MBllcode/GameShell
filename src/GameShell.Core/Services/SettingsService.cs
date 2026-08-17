using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private readonly string _backupPath;
    private readonly string _tempPath;
    private readonly LegacySettingsPropertyMigrator _legacySettingsPropertyMigrator;
    private AppSettings? _cachedSettings;
    private DateTime _cachedLastWriteTimeUtc;

    public SettingsService(
        ILogger<SettingsService> logger,
        string? settingsPath = null,
        LegacySettingsPropertyMigrator? legacySettingsPropertyMigrator = null)
    {
        _logger = logger;
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameShell",
            "settings.json");
        _backupPath = $"{SettingsPath}.bak";
        _tempPath = $"{SettingsPath}.tmp";
        _legacySettingsPropertyMigrator = legacySettingsPropertyMigrator ?? new LegacySettingsPropertyMigrator();
    }

    public string SettingsPath { get; }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        await _settingsGate.WaitAsync(cancellationToken);

        try
        {
            if (File.Exists(SettingsPath))
            {
                var lastWriteTimeUtc = File.GetLastWriteTimeUtc(SettingsPath);
                if (_cachedSettings is not null && _cachedLastWriteTimeUtc == lastWriteTimeUtc)
                {
                    return _cachedSettings;
                }

                var primary = await TryLoadValidSettingsAsync(SettingsPath, cancellationToken);
                if (primary.IsValid)
                {
                    if (primary.WasMigrated)
                    {
                        await PersistMigratedSettingsAsync(primary.Settings, cancellationToken);
                    }

                    _cachedSettings = primary.Settings;
                    _cachedLastWriteTimeUtc = File.GetLastWriteTimeUtc(SettingsPath);
                    LogRefreshPolicyHydration(primary.Settings, primary.Settings);
                    _logger.LogDebug(
                        "[SETTINGS LOAD] Path={Path} Result=Success",
                        SettingsPath);
                    return primary.Settings;
                }

                var corruptPath = $"{SettingsPath}.corrupt-{DateTimeOffset.Now:yyyyMMddHHmmss}";
                try
                {
                    File.Copy(SettingsPath, corruptPath, overwrite: true);
                    _logger.LogWarning(
                        "[SETTINGS RECOVERY] Primary=Invalid Backup={BackupExists} Action=PreservedCorruptPrimary",
                        File.Exists(_backupPath));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[SETTINGS RECOVERY] Primary=Invalid Backup={BackupExists} Action=CorruptCopyFailed",
                        File.Exists(_backupPath));
                }

                if (File.Exists(_backupPath))
                {
                    var backup = await TryLoadValidSettingsAsync(_backupPath, cancellationToken);
                    if (backup.IsValid)
                    {
                        var restored = backup.Settings;
                        await WriteAtomicAsync(SettingsPath, SerializeSettings(restored), cancellationToken);
                        _cachedSettings = restored;
                        _cachedLastWriteTimeUtc = File.GetLastWriteTimeUtc(SettingsPath);
                        _logger.LogWarning(
                            "[SETTINGS RECOVERY] Primary=Invalid Backup=Valid Action=RestoredBackup");
                        return restored;
                    }
                }

                var invalidDefaults = CreateDefaultSettings();
                await WriteSettingsAsync(invalidDefaults, cancellationToken);
                _logger.LogWarning(
                    "[SETTINGS RECOVERY] Primary=Invalid Backup=Invalid Action=Defaults");
                return invalidDefaults;
            }

            if (File.Exists(_backupPath))
            {
                var backup = await TryLoadValidSettingsAsync(_backupPath, cancellationToken);
                if (backup.IsValid)
                {
                    var restored = backup.Settings;
                    await WriteAtomicAsync(SettingsPath, SerializeSettings(restored), cancellationToken);
                    _cachedSettings = restored;
                    _cachedLastWriteTimeUtc = File.GetLastWriteTimeUtc(SettingsPath);
                    _logger.LogWarning(
                        "[SETTINGS RECOVERY] Primary=Missing Backup=Valid Action=RestoredBackup");
                    return restored;
                }
            }

            var missingDefaults = CreateDefaultSettings();
            await WriteSettingsAsync(missingDefaults, cancellationToken);
            _logger.LogDebug(
                "[SETTINGS LOAD] Primary=Missing Backup=Missing Action=Defaults Path={Path}",
                SettingsPath);
            return missingDefaults;
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        await _settingsGate.WaitAsync(cancellationToken);

        try
        {
            await WriteSettingsAsync(settings, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SETTINGS SAVE] Path={Path} Result=Failed", SettingsPath);
            throw;
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    private async Task WriteSettingsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var validated = AppSettingsValidator.Validate(settings);
        var payload = SerializeSettings(validated);

        if (File.Exists(SettingsPath))
        {
            var current = await TryLoadValidSettingsAsync(SettingsPath, cancellationToken);
            if (current.IsValid)
            {
                await BackupCurrentPrimaryAsync(cancellationToken);
            }
        }

        await WriteAtomicAsync(SettingsPath, payload, cancellationToken);
        _cachedSettings = validated;
        _cachedLastWriteTimeUtc = File.GetLastWriteTimeUtc(SettingsPath);
        LogRefreshPolicyHydration(validated, validated);
        _logger.LogInformation(
            "[SETTINGS SAVE] Path={Path} Bytes={Bytes} Result=Success",
            SettingsPath,
            payload.Length);
    }

    private async Task BackupCurrentPrimaryAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SettingsPath))
        {
            return;
        }

        var bytes = await File.ReadAllBytesAsync(SettingsPath, cancellationToken);
        await WriteAtomicAsync(_backupPath, bytes, cancellationToken);
    }

    private async Task WriteAtomicAsync(string destinationPath, string payload, CancellationToken cancellationToken)
    {
        await WriteAtomicAsync(destinationPath, Encoding.UTF8.GetBytes(payload), cancellationToken);
    }

    private async Task WriteAtomicAsync(string destinationPath, byte[] payload, CancellationToken cancellationToken)
    {
        var tempPath = $"{destinationPath}.tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        await using (var stream = new FileStream(
                         tempPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         4096,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await stream.WriteAsync(payload.AsMemory(0, payload.Length), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    private static string SerializeSettings(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        using var document = JsonDocument.Parse(json);
        _ = document.RootElement;
        var roundTrip = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
            ?? throw new InvalidOperationException("Serialized settings payload is invalid.");
        AppSettingsValidator.Validate(roundTrip);
        return json;
    }

    private async Task<(bool IsValid, AppSettings Settings, bool WasMigrated)> TryLoadValidSettingsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return (false, new AppSettings(), false);
        }

        try
        {
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var json = await reader.ReadToEndAsync(cancellationToken);
            json = _legacySettingsPropertyMigrator.Migrate(json);
            using var document = JsonDocument.Parse(json);
            var settings = document.RootElement.Deserialize<AppSettings>(JsonOptions)
                ?? new AppSettings();
            var wasMigrated = AppSettingsValidator.HasLegacyCapabilityData(settings);
            var validated = AppSettingsValidator.Validate(settings);
            return (true, validated, wasMigrated);
        }
        catch (Exception)
        {
            return (false, new AppSettings(), false);
        }
    }

    private async Task PersistMigratedSettingsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await BackupCurrentPrimaryAsync(cancellationToken);
        await WriteAtomicAsync(SettingsPath, SerializeSettings(settings), cancellationToken);
        _logger.LogInformation(
            "[SETTINGS MIGRATION] Path={Path} Migration=DisplayCapabilityCatalog Result=Success",
            SettingsPath);
    }

    private static AppSettings CreateDefaultSettings() => new();

    private void LogRefreshPolicyHydration(AppSettings? persisted, AppSettings resolved)
    {
        foreach (var profile in resolved.Display.GamingProfiles)
        {
            var persistedProfile = persisted?.Display.GamingProfiles
                .FirstOrDefault(candidate => string.Equals(candidate.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
            foreach (var target in profile.DisplayTopology?.Displays ?? [])
            {
                var persistedTarget = persistedProfile?.DisplayTopology?.Displays
                    .FirstOrDefault(candidate => string.Equals(candidate.Identity.MonitorDevicePath, target.Identity.MonitorDevicePath, StringComparison.OrdinalIgnoreCase));
                var persistedPolicy = persistedTarget?.RefreshPolicy?.ToString() ?? "<missing>";
                var persistedRefresh = persistedTarget is null
                    ? "<missing>"
                    : $"legacy={persistedTarget.RefreshRateNumerator}/{persistedTarget.RefreshRateDenominator};fixed={persistedTarget.FixedRefreshRateNumerator}/{persistedTarget.FixedRefreshRateDenominator}";
                var resolvedPolicy = target.EffectiveRefreshPolicy.ToString();
                var resolvedUiValue = resolvedPolicy == nameof(RefreshPolicy.AutoHighest)
                    ? "Najwyższe dostępne"
                    : $"{target.EffectiveFixedRefreshRateNumerator / (double)target.EffectiveFixedRefreshRateDenominator:0.##} Hz";
                _logger.LogDebug(
                    "[REFRESH POLICY HYDRATION] Profile={Profile} Display={Display} PersistedPolicy={PersistedPolicy} PersistedRefresh={PersistedRefresh} ResolvedDraftPolicy={ResolvedDraftPolicy} ResolvedUiValue={ResolvedUiValue}",
                    profile.Name,
                    target.Identity.FriendlyName,
                    persistedPolicy,
                    persistedRefresh,
                    resolvedPolicy,
                    resolvedUiValue);
            }
        }
    }
}

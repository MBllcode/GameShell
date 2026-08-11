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
    private AppSettings? _cachedSettings;
    private DateTime _cachedLastWriteTimeUtc;

    public SettingsService(ILogger<SettingsService> logger, string? settingsPath = null)
    {
        _logger = logger;
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameShell",
            "settings.json");
    }

    public string SettingsPath { get; }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        await _settingsGate.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(SettingsPath))
            {
                var defaults = CreateDefaultSettings();
                await WriteSettingsAsync(defaults, cancellationToken);
                _logger.LogDebug(
                    "FIRST RUN COMPLETION LOADED isFirstRunCompleted={IsFirstRunCompleted} settingsPath={SettingsPath}",
                    defaults.Ui.IsFirstRunCompleted,
                    SettingsPath);
                return defaults;
            }

            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(SettingsPath);
            if (_cachedSettings is not null && _cachedLastWriteTimeUtc == lastWriteTimeUtc)
            {
                return _cachedSettings;
            }

            try
            {
                await using var stream = File.OpenRead(SettingsPath);
                var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
                var validated = AppSettingsValidator.Validate(settings ?? CreateDefaultSettings());
                _cachedSettings = validated;
                _cachedLastWriteTimeUtc = lastWriteTimeUtc;
                LogRefreshPolicyHydration(settings, validated);
                _logger.LogDebug(
                    "FIRST RUN COMPLETION LOADED isFirstRunCompleted={IsFirstRunCompleted} settingsPath={SettingsPath}",
                    validated.Ui.IsFirstRunCompleted,
                    SettingsPath);
                return validated;
            }
            catch (JsonException ex)
            {
                var backupPath = $"{SettingsPath}.corrupt-{DateTimeOffset.Now:yyyyMMddHHmmss}";
                File.Copy(SettingsPath, backupPath, overwrite: true);
                _logger.LogError(ex, "Settings JSON is invalid. Backed up to {BackupPath}", backupPath);

                var defaults = CreateDefaultSettings();
                await WriteSettingsAsync(defaults, cancellationToken);
                return defaults;
            }
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
        finally
        {
            _settingsGate.Release();
        }
    }

    private async Task WriteSettingsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var validated = AppSettingsValidator.Validate(settings);
        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, validated, JsonOptions, cancellationToken);
        _cachedSettings = validated;
        _cachedLastWriteTimeUtc = File.GetLastWriteTimeUtc(SettingsPath);
        LogRefreshPolicyHydration(validated, validated);
        _logger.LogInformation(
            "FIRST RUN COMPLETION SAVED isFirstRunCompleted={IsFirstRunCompleted} settingsPath={SettingsPath}",
            validated.Ui.IsFirstRunCompleted,
            SettingsPath);
    }

    private static AppSettings CreateDefaultSettings() => new();

    private void LogRefreshPolicyHydration(AppSettings? persisted, AppSettings resolved)
    {
        foreach (var profile in resolved.Display.PcGamingProfiles)
        {
            var persistedProfile = persisted?.Display.PcGamingProfiles
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

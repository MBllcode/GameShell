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

        if (!File.Exists(SettingsPath))
        {
            var defaults = CreateDefaultSettings();
            await SaveAsync(defaults, cancellationToken);
            _logger.LogInformation(
                "FIRST RUN COMPLETION LOADED isFirstRunCompleted={IsFirstRunCompleted} settingsPath={SettingsPath}",
                defaults.Ui.IsFirstRunCompleted,
                SettingsPath);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            var validated = AppSettingsValidator.Validate(settings ?? CreateDefaultSettings());
            LogRefreshPolicyHydration(settings, validated);
            _logger.LogInformation(
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
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var validated = AppSettingsValidator.Validate(settings);
        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, validated, JsonOptions, cancellationToken);
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
                _logger.LogInformation(
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

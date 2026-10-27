using GameShell.Core.Displays;
using GameShell.Core.Settings;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameShell.Desktop.Displays;

/// <summary>
/// Keeps raw DisplayConfig observations behind a stable logical snapshot.
/// Hotplug settling is deliberately short and bounded: 300 ms cadence, 2 s
/// maximum, and two consecutive identity-stable observations.
/// </summary>
public sealed class DisplayDiscoveryCoordinator : IDisplayDiscoveryStabilizationService, IDisposable
{
    private const int SettlePassDelayMs = 300;
    private const int MaxSettleWindowMs = 2000;
    private const int RequiredStablePasses = 2;
    private static readonly TimeSpan ReadinessRefreshTimeout = TimeSpan.FromSeconds(3);

    private static readonly JsonSerializerOptions CloneJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ISettingsService _settingsService;
    private readonly IDisplayQueryService _displayQueryService;
    private readonly ILogger<DisplayDiscoveryCoordinator> _logger;
    private readonly SemaphoreSlim _discoveryGate = new(1, 1);
    private readonly object _stateGate = new();
    private IReadOnlyList<DisplayDeviceInfo> _lastStableDisplays = [];
    private Task<IReadOnlyList<DisplayDeviceInfo>>? _initialDiscoveryTask;
    private Task? _settlingTask;
    private TaskCompletionSource<bool> _settled = CompletedSource();
    private DateTimeOffset? _settlingStartedAt;
    private DisplayDiscoveryState _state = DisplayDiscoveryState.Idle;

    public DisplayDiscoveryCoordinator(
        ISettingsService settingsService,
        IDisplayQueryService displayQueryService,
        ILogger<DisplayDiscoveryCoordinator> logger)
    {
        _settingsService = settingsService;
        _displayQueryService = displayQueryService;
        _logger = logger;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public DisplayDiscoveryState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<DisplayDiscoveryStateChangedEventArgs>? StateChanged;

    public Task<IReadOnlyList<DisplayDeviceInfo>> GetDisplaysAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateGate)
        {
            if (_state == DisplayDiscoveryState.Settling && _lastStableDisplays.Count > 0)
            {
                return Task.FromResult(_lastStableDisplays);
            }

            if (_initialDiscoveryTask is not null)
            {
                return _initialDiscoveryTask;
            }

            _initialDiscoveryTask = LoadInitialSnapshotAsync(cancellationToken);
            return _initialDiscoveryTask;
        }
    }

    public void BeginSettling(string reason)
    {
        lock (_stateGate)
        {
            if (_state == DisplayDiscoveryState.Settling)
            {
                return;
            }

            _settlingStartedAt = DateTimeOffset.UtcNow;
            _settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetStateLocked(DisplayDiscoveryState.Settling, reason);
            _settlingTask = RunSettlingAsync(_settlingStartedAt.Value);
        }
    }

    public async Task WaitForSettledAsync(CancellationToken cancellationToken = default)
    {
        Task waitTask;
        lock (_stateGate)
        {
            waitTask = _state == DisplayDiscoveryState.Settling
                ? _settled.Task
                : Task.CompletedTask;
        }

        await waitTask.WaitAsync(cancellationToken);
    }

    public async Task WaitForStableAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        Task waitTask;
        lock (_stateGate)
        {
            waitTask = _state switch
            {
                DisplayDiscoveryState.Settling => _settled.Task,
                DisplayDiscoveryState.Loading when _initialDiscoveryTask is not null => _initialDiscoveryTask,
                _ => Task.CompletedTask
            };
        }

        await waitTask.WaitAsync(timeout, cancellationToken);
    }

    public async Task EnsureStableAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await GetDisplaysAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
        var remaining = timeout - stopwatch.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException("Initial display discovery did not settle within the configured timeout.");
        }

        await WaitForStableAsync(remaining, cancellationToken);
    }

    public async Task<IReadOnlyList<DisplayDeviceInfo>> GetStableDisplaysAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureStableAsync(ReadinessRefreshTimeout, cancellationToken);

        // DisplaySettingsChanged is not guaranteed to be raised for every
        // HDMI re-enumeration. A profile readiness check is an existing,
        // bounded discovery boundary, so use it to detect a changed physical
        // observation and hand the work back to the normal settle loop.
        if (State == DisplayDiscoveryState.Ready
            && await HasCurrentObservationChangedAsync(cancellationToken))
        {
            BeginSettling("ReadinessTopologyChanged");
            try
            {
                await WaitForStableAsync(ReadinessRefreshTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(
                    "[DISPLAY DISCOVERY] Readiness refresh did not settle within {TimeoutMs} ms; retaining the last safe snapshot.",
                    ReadinessRefreshTimeout.TotalMilliseconds);
            }
        }

        lock (_stateGate)
        {
            return _lastStableDisplays;
        }
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _discoveryGate.Dispose();
    }

    private async Task<IReadOnlyList<DisplayDeviceInfo>> LoadInitialSnapshotAsync(CancellationToken cancellationToken)
    {
        SetState(DisplayDiscoveryState.Loading, "InitialDiscovery");
        try
        {
            var snapshot = await DiscoverAsync(commit: true, deferTransientCandidates: false, cancellationToken);
            lock (_stateGate)
            {
                _lastStableDisplays = snapshot.Displays;
                SetStateLocked(DisplayDiscoveryState.Ready, "InitialDiscoveryComplete");
            }

            return snapshot.Displays;
        }
        catch
        {
            SetState(DisplayDiscoveryState.Error, "InitialDiscoveryFailed");
            throw;
        }
        finally
        {
            lock (_stateGate)
            {
                _initialDiscoveryTask = null;
            }
        }
    }

    private async Task RunSettlingAsync(DateTimeOffset startedAt)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        string? previousSignature = null;
        var stablePasses = 0;
        DiscoverySnapshot? latest = null;
        var attempt = 0;

        try
        {
            while (stopwatch.ElapsedMilliseconds <= MaxSettleWindowMs)
            {
                attempt++;
                latest = await DiscoverAsync(commit: false, deferTransientCandidates: true, CancellationToken.None);
                var signature = BuildStabilitySignature(latest.Merge);
                var stable = IsStable(latest.Merge)
                    && string.Equals(previousSignature, signature, StringComparison.Ordinal);
                stablePasses = stable ? stablePasses + 1 : 0;
                previousSignature = signature;

                _logger.LogInformation(
                    "[DISPLAY DISCOVERY SETTLE] Attempt={Attempt} PhysicalDisplays={PhysicalDisplays} KnownDisplays={KnownDisplays} Stable={Stable} Reason={Reason}",
                    attempt,
                    latest.Merge.ObservationCount,
                    latest.Merge.KnownCount,
                    stablePasses >= RequiredStablePasses,
                    GetSettleReason(latest.Merge));

                if (stablePasses >= RequiredStablePasses)
                {
                    var committed = await CommitSnapshotAsync(latest.CurrentDisplays, CancellationToken.None);
                    lock (_stateGate)
                    {
                        _lastStableDisplays = committed.Displays;
                    }

                    CompleteSettling(DisplayDiscoveryState.Ready, startedAt, "StableIdentity");
                    return;
                }

                if (stopwatch.ElapsedMilliseconds + SettlePassDelayMs > MaxSettleWindowMs)
                {
                    break;
                }

                await Task.Delay(SettlePassDelayMs);
            }

            if (latest is not null && IsSafeToCommitAfterTimeout(latest.Merge))
            {
                var committed = await CommitSnapshotAsync(latest.CurrentDisplays, CancellationToken.None);
                lock (_stateGate)
                {
                    _lastStableDisplays = committed.Displays;
                }
            }

            _logger.LogInformation(
                "[DISPLAY DISCOVERY SETTLE] Attempt={Attempt} PhysicalDisplays={PhysicalDisplays} KnownDisplays={KnownDisplays} Stable=False Reason=TimeoutSafeState",
                attempt,
                latest?.Merge.ObservationCount ?? 0,
                latest?.Merge.KnownCount ?? 0);
            CompleteSettling(DisplayDiscoveryState.Ready, startedAt, "Timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Display discovery settling failed after {DurationMs} ms.", stopwatch.ElapsedMilliseconds);
            CompleteSettling(DisplayDiscoveryState.Error, startedAt, "SettlingFailed");
        }
    }

    private async Task<DiscoverySnapshot> DiscoverAsync(
        bool commit,
        bool deferTransientCandidates,
        CancellationToken cancellationToken)
    {
        await _discoveryGate.WaitAsync(cancellationToken);
        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var currentDisplays = await _displayQueryService.GetAvailableDisplaysAsync(cancellationToken);
            currentDisplays = await RefreshCurrentModeInventoryAsync(currentDisplays, cancellationToken);

            var mergeSettings = commit ? settings.Display : Clone(settings.Display);
            var merge = KnownPhysicalDisplayCatalogService.Merge(
                mergeSettings,
                currentDisplays,
                deferTransientCandidates);

            LogMerge(merge);
            if (commit && merge.CatalogChanged)
            {
                await _settingsService.SaveAsync(settings, cancellationToken);
            }

            return new DiscoverySnapshot(currentDisplays, merge.Displays, merge);
        }
        finally
        {
            _discoveryGate.Release();
        }
    }

    private async Task<DiscoverySnapshot> CommitSnapshotAsync(
        IReadOnlyList<DisplayDeviceInfo> currentDisplays,
        CancellationToken cancellationToken)
    {
        await _discoveryGate.WaitAsync(cancellationToken);
        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var merge = KnownPhysicalDisplayCatalogService.Merge(settings.Display, currentDisplays);
            LogMerge(merge);
            if (merge.CatalogChanged)
            {
                await _settingsService.SaveAsync(settings, cancellationToken);
            }

            return new DiscoverySnapshot(currentDisplays, merge.Displays, merge);
        }
        finally
        {
            _discoveryGate.Release();
        }
    }

    private async Task<IReadOnlyList<DisplayDeviceInfo>> RefreshCurrentModeInventoryAsync(
        IReadOnlyList<DisplayDeviceInfo> displays,
        CancellationToken cancellationToken)
    {
        var refreshed = new List<DisplayDeviceInfo>(displays.Count);
        foreach (var display in displays)
        {
            if (!display.IsEnabled || !display.IsAvailable)
            {
                refreshed.Add(display);
                continue;
            }

            try
            {
                var inventory = await _displayQueryService.GetAvailableDisplayModesAsync(display.Identity, cancellationToken);
                var requestedModes = inventory.Modes
                    .Where(mode => mode.Width == display.Width && mode.Height == display.Height)
                    .OrderByDescending(mode => mode.RefreshRateHz)
                    .ToList();
                var highest = requestedModes.FirstOrDefault();
                refreshed.Add(highest is null ? display : WithCurrentCapability(display, highest, inventory.Modes));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[DISPLAY MODE INVENTORY] Refresh failed; keeping DisplayConfig observation.");
                refreshed.Add(display);
            }
        }

        return refreshed;
    }

    private async Task<bool> HasCurrentObservationChangedAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DisplayDeviceInfo> stable;
        lock (_stateGate)
        {
            stable = _lastStableDisplays;
        }

        if (stable.Count == 0)
        {
            return false;
        }

        try
        {
            var current = await _displayQueryService.GetAvailableDisplaysAsync(cancellationToken);
            return !string.Equals(
                BuildObservationSignature(current),
                BuildStableObservationSignature(stable),
                StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[DISPLAY DISCOVERY] Could not probe for a readiness-time topology change.");
            return false;
        }
    }

    private static string BuildObservationSignature(IReadOnlyList<DisplayDeviceInfo> displays)
    {
        return string.Join(
            "|",
            displays
                .Select(display => FormatObservationSignature(
                    display.Name,
                    display.IsEnabled,
                    display.IsAvailable,
                    display.Identity))
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string BuildStableObservationSignature(IReadOnlyList<DisplayDeviceInfo> displays)
    {
        return string.Join(
            "|",
            displays
                .Select(display =>
                {
                    var observation = display.CurrentObservation;
                    return FormatObservationSignature(
                        observation?.CurrentObservedName ?? display.Name,
                        observation?.IsActive ?? display.IsEnabled,
                        observation?.IsAvailable ?? display.IsAvailable,
                        observation?.Identity ?? display.Identity);
                })
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string FormatObservationSignature(
        string name,
        bool isActive,
        bool isAvailable,
        DisplayIdentity identity)
    {
        return string.Join(
            ":",
            name,
            isActive,
            isAvailable,
            identity.AdapterLuid,
            identity.TargetId,
            identity.MonitorDevicePath,
            identity.EdidIdsValid,
            identity.EdidManufacturerId,
            identity.EdidProductCodeId,
            identity.ConnectorInstance,
            identity.OutputTechnology);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        BeginSettling("TopologyChanged");
    }

    private void SetState(DisplayDiscoveryState state, string reason)
    {
        lock (_stateGate)
        {
            SetStateLocked(state, reason);
        }
    }

    private void SetStateLocked(DisplayDiscoveryState state, string reason)
    {
        if (_state == state)
        {
            return;
        }

        var oldState = _state;
        _state = state;
        _logger.LogInformation(
            "[DISPLAY DISCOVERY STATE] OldState={OldState} NewState={NewState} Reason={Reason}",
            oldState,
            state,
            reason);
        StateChanged?.Invoke(this, new DisplayDiscoveryStateChangedEventArgs(oldState, state, reason, null));
    }

    private void CompleteSettling(DisplayDiscoveryState state, DateTimeOffset startedAt, string reason)
    {
        var durationMs = (long)Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
        lock (_stateGate)
        {
            var oldState = _state;
            _state = state;
            _logger.LogInformation(
                "[DISPLAY DISCOVERY STATE] OldState={OldState} NewState={NewState} Reason={Reason} DurationMs={DurationMs}",
                oldState,
                state,
                reason,
                durationMs);
            StateChanged?.Invoke(this, new DisplayDiscoveryStateChangedEventArgs(oldState, state, reason, durationMs));
            _settled.TrySetResult(true);
        }
    }

    private static bool IsStable(DisplayCatalogMergeResult merge)
    {
        return merge.DeferredCount == 0
            && merge.ConflictCount == 0
            && merge.IdentityMatches.All(match => !match.MissingMetadata && !string.Equals(match.Result, "Conflict", StringComparison.Ordinal));
    }

    private static bool IsSafeToCommitAfterTimeout(DisplayCatalogMergeResult merge)
    {
        // A reconnect candidate or an identity conflict is kept transient. A
        // genuinely new observation with complete strong metadata may be
        // admitted after the bounded window.
        return merge.DeferredCount == 0 && merge.ConflictCount == 0;
    }

    private static string BuildStabilitySignature(DisplayCatalogMergeResult merge)
    {
        return string.Join(
            "|",
            merge.IdentityMatches
                .Select(match =>
                {
                    var id = string.Equals(match.Result, "New", StringComparison.Ordinal)
                        || string.Equals(match.Result, "Deferred", StringComparison.Ordinal)
                        ? $"target:{match.AdapterLuid}:{match.TargetId}:{match.Result}"
                        : string.IsNullOrWhiteSpace(match.KnownDisplayId) ? $"target:{match.AdapterLuid}:{match.TargetId}" : match.KnownDisplayId;
                    return $"{id}:{match.MatchConfidence}:{match.MissingMetadata}";
                })
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string GetSettleReason(DisplayCatalogMergeResult merge)
    {
        if (merge.DeferredCount > 0) return "IdentityPending";
        if (merge.ConflictCount > 0) return "IdentityConflict";
        if (merge.NewCount > 0) return "NewIdentityPending";
        return "SnapshotChanged";
    }

    private void LogMerge(DisplayCatalogMergeResult merge)
    {
        _logger.LogInformation(
            "[DISPLAY DISCOVERY MERGE] KnownCount={KnownCount}, ObservationCount={ObservationCount}, LogicalCount={LogicalCount}, DuplicatesCollapsed={DuplicatesCollapsed}",
            merge.KnownCount,
            merge.ObservationCount,
            merge.LogicalCount,
            merge.DuplicatesCollapsed);
        foreach (var match in merge.IdentityMatches.Where(match => string.Equals(match.Result, "Deferred", StringComparison.Ordinal)))
        {
            _logger.LogInformation(
                "[DISPLAY IDENTITY DEFERRED] ObservedName={ObservedName} TargetId={TargetId} Reason=SettlingIncompleteIdentity",
                match.ObservedName,
                match.TargetId);
        }
    }

    private static DisplaySettings Clone(DisplaySettings settings)
    {
        var json = JsonSerializer.Serialize(settings, CloneJsonOptions);
        return JsonSerializer.Deserialize<DisplaySettings>(json, CloneJsonOptions) ?? new DisplaySettings();
    }

    private static DisplayDeviceInfo WithCurrentCapability(
        DisplayDeviceInfo display,
        DisplayModeInfo capability,
        IReadOnlyList<DisplayModeInfo> enumeratedModes)
    {
        return new DisplayDeviceInfo
        {
            Id = display.Id,
            KnownPhysicalDisplayId = display.KnownPhysicalDisplayId,
            Name = display.Name,
            IsPrimary = display.IsPrimary,
            IsEnabled = display.IsEnabled,
            IsAvailable = display.IsAvailable,
            Identity = display.Identity,
            SourceDeviceName = display.SourceDeviceName,
            SourceId = display.SourceId,
            TargetId = display.TargetId,
            AdapterLuid = display.AdapterLuid,
            ConnectionTechnology = display.ConnectionTechnology,
            PositionX = display.PositionX,
            PositionY = display.PositionY,
            Width = display.Width,
            Height = display.Height,
            Orientation = display.Orientation,
            RefreshRateNumerator = capability.RefreshRateNumerator,
            RefreshRateDenominator = capability.RefreshRateDenominator,
            IsRemembered = display.IsRemembered,
            CatalogState = display.CatalogState,
            CurrentObservation = display.CurrentObservation,
            EnumeratedModes = enumeratedModes,
            LastKnownGoodCapability = display.LastKnownGoodCapability,
            LastVerifiedActiveCapability = display.LastVerifiedActiveCapability
        };
    }

    private static TaskCompletionSource<bool> CompletedSource()
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult(true);
        return source;
    }

    private sealed record DiscoverySnapshot(
        IReadOnlyList<DisplayDeviceInfo> CurrentDisplays,
        IReadOnlyList<DisplayDeviceInfo> Displays,
        DisplayCatalogMergeResult Merge);
}

public sealed class DisplayDiscoveryStateChangedEventArgs : EventArgs
{
    public DisplayDiscoveryStateChangedEventArgs(
        DisplayDiscoveryState oldState,
        DisplayDiscoveryState newState,
        string reason,
        long? durationMs)
    {
        OldState = oldState;
        NewState = newState;
        Reason = reason;
        DurationMs = durationMs;
    }

    public DisplayDiscoveryState OldState { get; }
    public DisplayDiscoveryState NewState { get; }
    public string Reason { get; }
    public long? DurationMs { get; }
}

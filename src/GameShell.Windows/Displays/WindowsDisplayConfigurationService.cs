using System.Runtime.InteropServices;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Displays;

public sealed class WindowsDisplayConfigurationService : IDisplayConfigurationService
{
    private const int MaxQueryAttempts = 3;
    private static readonly TimeSpan RefreshRateCorrectionDelay = TimeSpan.FromMilliseconds(750);
    private readonly ILogger<WindowsDisplayConfigurationService> _logger;

    public WindowsDisplayConfigurationService(ILogger<WindowsDisplayConfigurationService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<DisplayDeviceInfo>> GetAvailableDisplaysAsync(CancellationToken cancellationToken = default)
    {
        var topology = QueryAvailableTopology(cancellationToken);
        var mapper = new DisplayConfigMapper(_logger);
        var devices = mapper.MapDevices(topology);
        _logger.LogInformation(
            "[DISPLAY INVENTORY] AvailablePaths={AvailablePaths} PhysicalDisplays={PhysicalDisplays} ActiveDisplays={ActiveDisplays} Displays=[{Displays}]",
            topology.Paths.Count,
            devices.Count,
            devices.Count(device => device.IsEnabled),
            string.Join(", ", devices.Select(device => $"{device.Name ?? device.Identity.FriendlyName ?? "(unnamed)"}({(device.IsEnabled ? "active" : "inactive")})")));
        foreach (var device in devices)
        {
            _logger.LogDebug(
                "[DISPLAY MODE ENUM SOURCE] FriendlyName={FriendlyName} AdapterLuid={AdapterLuid} TargetId={TargetId} SourceId={SourceId} SourceDeviceName={SourceDeviceName} Active={Active} Available={Available} EnumDisplaySettingsDeviceName={EnumDisplaySettingsDeviceName} CurrentMode={CurrentMode}",
                device.Name,
                device.AdapterLuid,
                device.TargetId,
                device.SourceId,
                device.SourceDeviceName,
                device.IsEnabled,
                device.IsAvailable,
                device.IsEnabled ? device.SourceDeviceName : "-",
                FormatMode(device.Width, device.Height, device.RefreshRateHz));
        }
        return Task.FromResult(devices);
    }

    public Task<DisplayLayoutSnapshot> CaptureCurrentLayoutAsync(CancellationToken cancellationToken = default)
    {
        var topology = QueryActiveTopology(cancellationToken);
        var mapper = new DisplayConfigMapper(_logger);
        var snapshot = mapper.MapSnapshot(topology);
        _logger.LogInformation("Captured DisplayConfig layout with {DisplayCount} active display path(s).", snapshot.Displays.Count);
        return Task.FromResult(snapshot);
    }

    public Task<DisplayModeQueryResult> GetAvailableDisplayModesAsync(DisplayIdentity display, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var activeTopology = QueryActiveTopology(cancellationToken);
        var mapper = new DisplayConfigMapper(_logger);
        var activeDevices = mapper.MapDevices(activeTopology);
        var activeMatch = FindUniqueDisplay(activeDevices, display);
        var topology = activeMatch is not null
            ? activeTopology
            : QueryAvailableTopology(cancellationToken);
        var devices = mapper.MapDevices(topology);
        var candidates = devices
            .Select(device => new { Device = device, Score = DisplayIdentityMatcher.GetStableIdentityScore(display, device.Identity) })
            .Where(candidate => candidate.Score >= 35)
            .OrderByDescending(candidate => candidate.Score)
            .ToList();
        if (candidates.Count == 0 || (candidates.Count > 1 && candidates[0].Score == candidates[1].Score))
        {
            return Task.FromResult(new DisplayModeQueryResult());
        }

        var selectedDevice = candidates[0].Device;
        var sourceDeviceName = activeMatch is not null
            ? activeMatch.SourceDeviceName
            : string.Empty;
        _logger.LogDebug(
            "[DISPLAY MODE ENUM SOURCE] FriendlyName={FriendlyName} AdapterLuid={AdapterLuid} TargetId={TargetId} SourceId={SourceId} SourceDeviceName={SourceDeviceName} Active={Active} Available={Available} EnumDisplaySettingsDeviceName={EnumDisplaySettingsDeviceName} CurrentMode={CurrentMode}",
            selectedDevice.Name,
            selectedDevice.AdapterLuid,
            selectedDevice.TargetId,
            selectedDevice.SourceId,
            selectedDevice.SourceDeviceName,
            selectedDevice.IsEnabled,
            selectedDevice.IsAvailable,
            string.IsNullOrWhiteSpace(sourceDeviceName) ? "-" : sourceDeviceName,
            FormatMode(selectedDevice.Width, selectedDevice.Height, selectedDevice.RefreshRateHz));
        if (string.IsNullOrWhiteSpace(sourceDeviceName))
        {
            return Task.FromResult(new DisplayModeQueryResult { InventoryValid = activeMatch is null });
        }

        var modes = EnumerateDisplayModes(sourceDeviceName)
            .Where(mode => mode.PelsWidth > 0 && mode.PelsHeight > 0 && mode.DisplayFrequency > 0)
            .Select(mode => new DisplayModeInfo
            {
                Width = checked((int)mode.PelsWidth),
                Height = checked((int)mode.PelsHeight),
                RefreshRateNumerator = mode.DisplayFrequency,
                RefreshRateDenominator = 1
            })
            .ToList();
        var currentMode = activeMatch;
        if (currentMode is not null
            && currentMode.Width > 0
            && currentMode.Height > 0
            && currentMode.RefreshRateNumerator > 0
            && currentMode.RefreshRateDenominator > 0
            && !modes.Any(mode => SameMode(mode, currentMode)))
        {
            modes.Add(new DisplayModeInfo
            {
                Width = currentMode.Width,
                Height = currentMode.Height,
                RefreshRateNumerator = currentMode.RefreshRateNumerator,
                RefreshRateDenominator = currentMode.RefreshRateDenominator
            });
        }
        modes = modes
            .GroupBy(mode => $"{mode.Width}:{mode.Height}:{mode.RefreshRateNumerator}:{mode.RefreshRateDenominator}")
            .Select(group => group.First())
            .ToList();
        var requestedResolutionRates = modes
            .Where(mode => currentMode is not null && mode.Width == currentMode.Width && mode.Height == currentMode.Height)
            .Select(mode => mode.RefreshRateHz)
            .Distinct()
            .OrderBy(rate => rate)
            .Select(rate => $"{rate:0.##}Hz")
            .ToArray();
        _logger.LogInformation(
            "[DISPLAY MODE INVENTORY] Display={Display} CurrentMode={CurrentMode} ModeCount={ModeCount} RequestedResolutionRefreshRates=[{RefreshRates}]",
            currentMode?.Name ?? display.FriendlyName,
            FormatMode(currentMode?.Width ?? 0, currentMode?.Height ?? 0, currentMode?.RefreshRateHz ?? 0),
            modes.Count,
            string.Join(", ", requestedResolutionRates));
        _logger.LogDebug(
            "[DISPLAY MODE INVENTORY DETAILS] Display={Display} AvailableModes={AvailableModes}",
            currentMode?.Name ?? display.FriendlyName,
            FormatModes(modes));
        return Task.FromResult(new DisplayModeQueryResult { Success = modes.Count > 0, Modes = modes });
    }

    private static DisplayDeviceInfo? FindUniqueDisplay(IReadOnlyList<DisplayDeviceInfo> devices, DisplayIdentity identity)
    {
        var matches = devices
            .Where(device => DisplayIdentityMatcher.IsReliableMatch(identity, device.Identity))
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool SameMode(DisplayModeInfo mode, DisplayDeviceInfo current)
    {
        if (mode.Width != current.Width || mode.Height != current.Height)
        {
            return false;
        }

        if (mode.RefreshRateDenominator == 0 || current.RefreshRateDenominator == 0)
        {
            return false;
        }

        return Math.Abs(mode.RefreshRateHz - current.RefreshRateHz) <= 0.25;
    }

    public Task<DisplayOperationResult> ValidateLayoutAsync(DisplayLayoutSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var normalizedSnapshot = DisplayLayoutTopologyNormalizer.NormalizePrimaryOrigin(snapshot);
        return Task.FromResult(ApplyLayout(normalizedSnapshot, validateOnly: true, cancellationToken));
    }

    public Task<DisplayOperationResult> ApplyLayoutAsync(DisplayLayoutSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var normalizedSnapshot = DisplayLayoutTopologyNormalizer.NormalizePrimaryOrigin(snapshot);
        var validation = ApplyLayout(normalizedSnapshot, validateOnly: true, cancellationToken);
        if (!validation.Success)
        {
            return Task.FromResult(validation);
        }

        var result = ApplyLayout(normalizedSnapshot, validateOnly: false, cancellationToken);
        if (result.Success)
        {
            foreach (var display in normalizedSnapshot.Displays.Where(display => display.IsActive && display.RefreshRateDenominator == 0))
            {
                TryApplyHighestRefreshRateForDisplay(display.Identity, cancellationToken);
            }
        }

        return Task.FromResult(result);
    }

    public Task<DisplayOperationResult> ActivateOnlyAsync(DisplayIdentity display, CancellationToken cancellationToken = default)
    {
        return ActivateOnlyAsync([display], cancellationToken);
    }

    public async Task<DisplayOperationResult> ActivateOnlyAsync(IReadOnlyCollection<DisplayIdentity> displays, CancellationToken cancellationToken = default)
    {
        if (displays.Count == 0)
        {
            return new DisplayOperationResult
            {
                Success = false,
                Message = "Nie wybrano ekranów Console Mode."
            };
        }

        var topology = QueryAvailableTopology(cancellationToken);
        var mapper = new DisplayConfigMapper(_logger);
        var available = mapper.MapSnapshot(topology);
        var selected = available.Displays
            .Where(path => displays.Any(display => DisplayIdentityMatcher.IsReliableMatch(display, path.Identity)))
            .OrderBy(path => path.PositionX)
            .ThenBy(path => path.PositionY)
            .ToList();
        if (selected.Count != displays.Count)
        {
            return new DisplayOperationResult
            {
                Success = false,
                Message = "Nie wszystkie wybrane ekrany Console Mode są obecnie dostępne."
            };
        }

        var primary = selected.FirstOrDefault(path => path.IsPrimary) ?? selected[0];
        var originX = primary.PositionX;
        var originY = primary.PositionY;
        var snapshot = new DisplayLayoutSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow,
            Displays = selected.Select(path => new DisplayPathSnapshot
            {
                Identity = path.Identity,
                IsActive = true,
                IsPrimary = DisplayIdentityMatcher.IsReliableMatch(primary.Identity, path.Identity),
                PositionX = path.PositionX - originX,
                PositionY = path.PositionY - originY,
                Width = path.Width,
                Height = path.Height,
                Orientation = path.Orientation,
                RefreshRateNumerator = path.RefreshRateNumerator,
                RefreshRateDenominator = path.RefreshRateDenominator,
                SourceId = path.SourceId,
                TargetId = path.TargetId,
                AdapterLuid = path.AdapterLuid,
                SourceDeviceName = path.SourceDeviceName,
                ConnectionTechnology = path.ConnectionTechnology
            }).ToList()
        };

        var result = await ApplyLayoutAsync(snapshot, cancellationToken);
        if (!result.Success)
        {
            return result;
        }

        if (selected.Count == 1)
        {
            TryApplyHighestRefreshRateForDisplay(selected[0].Identity, cancellationToken);
        }
        return result;
    }

    private void TryApplyHighestRefreshRateForDisplay(DisplayIdentity selectedDisplay, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.WaitHandle.WaitOne(RefreshRateCorrectionDelay))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var activeTopology = QueryActiveTopology(cancellationToken);
            var activePath = activeTopology.Paths
                .Where(path => path.IsActive)
                .FirstOrDefault(path => DisplayIdentityMatcher.IsReliableMatch(selectedDisplay, ToIdentity(path)));
            if (activePath is null)
            {
                _logger.LogDebug(
                    "Skipping display refresh-rate correction because the requested display is not active.");
                return;
            }

            if (string.IsNullOrWhiteSpace(activePath.SourceDeviceName) || activePath.Width <= 0 || activePath.Height <= 0)
            {
                _logger.LogWarning(
                    "Skipping Console refresh-rate correction because active display data is incomplete. Source: {SourceDeviceName}, Width: {Width}, Height: {Height}.",
                    activePath.SourceDeviceName,
                    activePath.Width,
                    activePath.Height);
                return;
            }

            var modes = EnumerateDisplayModes(activePath.SourceDeviceName);
            var bestMode = DisplayModeSelector.SelectHighestRefreshMode(
                modes,
                checked((uint)activePath.Width),
                checked((uint)activePath.Height));
            if (bestMode is null)
            {
                _logger.LogWarning(
                    "No GDI display mode found for Console display {FriendlyName} at {Width}x{Height}.",
                    activePath.MonitorFriendlyName,
                    activePath.Width,
                    activePath.Height);
                return;
            }

            var currentHz = activePath.RefreshRateHz();
            if (bestMode.Value.DisplayFrequency <= currentHz + 1)
            {
                _logger.LogInformation(
                    "Console display refresh-rate already uses the best detected mode. Display: {FriendlyName}, CurrentHz: {CurrentHz}, BestHz: {BestHz}.",
                    activePath.MonitorFriendlyName,
                    currentHz,
                    bestMode.Value.DisplayFrequency);
                return;
            }

            var modeToApply = bestMode.Value;
            modeToApply.Fields |= DisplayConfigNative.DmBitsPerPel
                | DisplayConfigNative.DmPelsWidth
                | DisplayConfigNative.DmPelsHeight
                | DisplayConfigNative.DmDisplayFrequency;

            var result = DisplayConfigNative.ChangeDisplaySettingsEx(
                activePath.SourceDeviceName,
                ref modeToApply,
                IntPtr.Zero,
                DisplayConfigNative.CdsUpdateRegistry,
                IntPtr.Zero);

            if (result != DisplayConfigNative.DispChangeSuccessful)
            {
                _logger.LogWarning(
                    "ChangeDisplaySettingsEx rejected Console refresh-rate correction. Display: {FriendlyName}, Source: {SourceDeviceName}, TargetHz: {TargetHz}, Result: {Result}.",
                    activePath.MonitorFriendlyName,
                    activePath.SourceDeviceName,
                    modeToApply.DisplayFrequency,
                    result);
                return;
            }

            _logger.LogInformation(
                "Applied Console display refresh-rate correction. Display: {FriendlyName}, Source: {SourceDeviceName}, Resolution: {Width}x{Height}, Hz: {RefreshHz}.",
                activePath.MonitorFriendlyName,
                activePath.SourceDeviceName,
                modeToApply.PelsWidth,
                modeToApply.PelsHeight,
                modeToApply.DisplayFrequency);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Console refresh-rate correction failed after DisplayConfig apply. Keeping Console Mode active.");
        }
    }

    private static IReadOnlyList<DevMode> EnumerateDisplayModes(string sourceDeviceName)
    {
        var modes = new List<DevMode>();
        for (uint index = 0; ; index++)
        {
            var mode = DevMode.Create();
            if (!DisplayConfigNative.EnumDisplaySettings(sourceDeviceName, index, ref mode))
            {
                break;
            }

            modes.Add(mode);
        }

        return modes;
    }

    private DisplayOperationResult ApplyLayout(DisplayLayoutSnapshot snapshot, bool validateOnly, CancellationToken cancellationToken)
    {
        var activeSnapshotDisplays = snapshot.Displays.Where(display => display.IsActive).ToList();
        var geometryValidation = ValidateSnapshotGeometry(snapshot);
        if (!geometryValidation.Success)
        {
            LogTopologyApply(snapshot, geometryValidation.Message);
            return geometryValidation;
        }

        if (activeSnapshotDisplays.Count == 0)
        {
            return new DisplayOperationResult { Success = false, Message = "Uklad ekranow nie zawiera zadnego aktywnego ekranu." };
        }

        var topology = QueryLayoutApplicationTopology(cancellationToken);
        if (!TryBuildNativeConfiguration(snapshot, topology, out var paths, out var modes, out var message))
        {
            LogTopologyApply(snapshot, message);
            return new DisplayOperationResult { Success = false, Message = message };
        }

        LogTopologyApply(snapshot, "OK");

        var flags = DisplayConfigNative.SdcUseSuppliedDisplayConfig
            | DisplayConfigNative.SdcAllowChanges
            | (validateOnly
                ? DisplayConfigNative.SdcValidate
                : DisplayConfigNative.SdcApply | DisplayConfigNative.SdcSaveToDatabase);

        var result = DisplayConfigNative.SetDisplayConfig(
            (uint)paths.Length,
            paths,
            (uint)modes.Length,
            modes,
            flags);

        if (result != DisplayConfigNative.ErrorSuccess)
        {
            _logger.LogError(
                "WORK MODE TRANSITION [{TransitionId}] SetDisplayConfig failed. ValidateOnly={ValidateOnly}, ResultCode={ResultCode}, PathCount={PathCount}, ModeCount={ModeCount}, Flags={Flags}.",
                WorkModeTransitionContext.CurrentId ?? "-",
                validateOnly,
                result,
                paths.Length,
                modes.Length,
                flags);
            return new DisplayOperationResult
            {
                Success = false,
                Message = $"Windows odrzucil konfiguracje ekranow. Kod bledu: {result}."
            };
        }

        _logger.LogInformation(
            "WORK MODE TRANSITION [{TransitionId}] SetDisplayConfig {Operation} succeeded. PathCount={PathCount}, ModeCount={ModeCount}, Flags={Flags}.",
            WorkModeTransitionContext.CurrentId ?? "-",
            validateOnly ? "validation" : "apply",
            paths.Length,
            modes.Length,
            flags);

        return new DisplayOperationResult
        {
            Success = true,
            Message = validateOnly ? "Uklad ekranow jest poprawny." : "Zastosowano uklad ekranow."
        };
    }

    private static DisplayOperationResult ValidateSnapshotGeometry(DisplayLayoutSnapshot snapshot)
    {
        var active = snapshot.Displays.Where(display => display.IsActive).ToList();
        if (active.Count == 0)
        {
            return new DisplayOperationResult { Success = false, Message = "Uklad ekranow nie zawiera zadnego aktywnego ekranu." };
        }

        if (active.Count(display => display.IsPrimary) != 1)
        {
            return new DisplayOperationResult { Success = false, Message = "Topologia musi zawierac dokładnie jeden główny ekran." };
        }

        var primary = active.Single(display => display.IsPrimary);
        if (primary.PositionX != 0 || primary.PositionY != 0)
        {
            return new DisplayOperationResult { Success = false, Message = "Główny ekran topologii nie znajduje się w punkcie 0,0." };
        }

        if (active.Any(display => display.Width <= 0 || display.Height <= 0))
        {
            return new DisplayOperationResult { Success = false, Message = "Topologia zawiera ekran bez poprawnego rozmiaru." };
        }

        if (active
            .Where(display => display.Identity.AdapterLuid != 0 && display.Identity.TargetId != 0)
            .GroupBy(display => $"{display.Identity.AdapterLuid}:{display.Identity.TargetId}")
            .Any(group => group.Count() > 1))
        {
            return new DisplayOperationResult { Success = false, Message = "Topologia zawiera zduplikowany cel fizyczny." };
        }

        for (var leftIndex = 0; leftIndex < active.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < active.Count; rightIndex++)
            {
                var left = active[leftIndex];
                var right = active[rightIndex];
                if (left.PositionX < right.PositionX + right.Width
                    && left.PositionX + left.Width > right.PositionX
                    && left.PositionY < right.PositionY + right.Height
                    && left.PositionY + left.Height > right.PositionY)
                {
                    return new DisplayOperationResult { Success = false, Message = $"Topologia zawiera nakładające się ekrany: {left.Identity.FriendlyName} i {right.Identity.FriendlyName}." };
                }
            }
        }

        return new DisplayOperationResult { Success = true, Message = "OK" };
    }

    private void LogTopologyApply(DisplayLayoutSnapshot snapshot, string validationResult)
    {
        var active = snapshot.Displays.Where(display => display.IsActive).ToList();
        var bounds = active.Count == 0
            ? "empty"
            : $"{active.Min(display => display.PositionX)},{active.Min(display => display.PositionY)}..{active.Max(display => display.PositionX + display.Width)},{active.Max(display => display.PositionY + display.Height)}";
        _logger.LogInformation("[DISPLAY TOPOLOGY APPLY] Profile={Profile} TopologyBounds={TopologyBounds} ValidationResult={ValidationResult}", "runtime", bounds, validationResult);
        foreach (var display in active)
        {
            _logger.LogInformation(
                "[DISPLAY TOPOLOGY APPLY] Name={Name} Primary={Primary} ResolvedSource={ResolvedSource} AdapterLuid={AdapterLuid} TargetId={TargetId} X={X} Y={Y} Width={Width} Height={Height} RefreshPolicy={RefreshPolicy}",
                display.Identity.FriendlyName,
                display.IsPrimary,
                display.SourceDeviceName,
                display.Identity.AdapterLuid,
                display.Identity.TargetId,
                display.PositionX,
                display.PositionY,
                display.Width,
                display.Height,
                display.RefreshRateDenominator == 0 ? "AutoHighest" : "Fixed");
        }
    }

    private static bool TryBuildNativeConfiguration(
        DisplayLayoutSnapshot snapshot,
        NativeDisplayTopology topology,
        out DisplayConfigPathInfo[] paths,
        out DisplayConfigModeInfo[] modes,
        out string message)
    {
        var activeDisplays = snapshot.Displays.Where(display => display.IsActive).ToList();
        var candidateSets = new List<(DisplayPathSnapshot Snapshot, IReadOnlyList<NativeDisplayPath> Candidates)>();

        foreach (var snapshotDisplay in activeDisplays)
        {
            var candidates = topology.Paths
                .Where(current => current.IsActive || current.IsAvailable)
                .Where(current => DisplayIdentityMatcher.IsReliableMatch(snapshotDisplay.Identity, ToIdentity(current)))
                .ToList();

            if (candidates.Count == 0)
            {
                paths = [];
                modes = [];
                message = $"Nie znaleziono wymaganego ekranu: {snapshotDisplay.Identity.FriendlyName}.";
                return false;
            }

            candidateSets.Add((snapshotDisplay, candidates));
        }

        if (!TrySelectUniqueSourceCandidates(candidateSets, out var matched))
        {
            paths = [];
            modes = [];
            message = "Nie można przypisać osobnej ścieżki Windows do każdego wybranego monitora.";
            return false;
        }

        var displayModes = new List<DisplayConfigModeInfo>();
        var targetPaths = new List<DisplayConfigPathInfo>();

        foreach (var (snapshotDisplay, currentPath) in matched)
        {
            var width = ResolveWidth(snapshotDisplay, currentPath);
            var height = ResolveHeight(snapshotDisplay, currentPath);
            if (width <= 0 || height <= 0)
            {
                paths = [];
                modes = [];
                message = $"Nie mozna ustalic rozdzielczosci dla ekranu: {snapshotDisplay.Identity.FriendlyName}.";
                return false;
            }

            var sourceModeIndex = (uint)displayModes.Count;
            displayModes.Add(new DisplayConfigModeInfo
            {
                InfoType = DisplayConfigModeInfoType.Source,
                AdapterId = currentPath.PathInfo.SourceInfo.AdapterId,
                Id = currentPath.PathInfo.SourceInfo.Id,
                ModeInfo = new DisplayConfigModeInfoUnion
                {
                    SourceMode = new DisplayConfigSourceMode
                    {
                        Width = checked((uint)width),
                        Height = checked((uint)height),
                        PixelFormat = currentPath.SourceMode.PixelFormat,
                        Position = new PointL
                        {
                            X = snapshotDisplay.PositionX,
                            Y = snapshotDisplay.PositionY
                        }
                    }
                }
            });

            var targetModeIndex = (uint)displayModes.Count;
            displayModes.Add(new DisplayConfigModeInfo
            {
                InfoType = DisplayConfigModeInfoType.Target,
                AdapterId = currentPath.PathInfo.TargetInfo.AdapterId,
                Id = currentPath.PathInfo.TargetInfo.Id,
                ModeInfo = new DisplayConfigModeInfoUnion
                {
                    TargetMode = BuildTargetMode(snapshotDisplay, currentPath, width, height)
                }
            });

            var path = currentPath.PathInfo;
            path.Flags = DisplayConfigNative.DisplayConfigPathActive;
            path.SourceInfo.ModeInfoIdx = sourceModeIndex;
            path.SourceInfo.StatusFlags = 0;
            path.TargetInfo.ModeInfoIdx = targetModeIndex;
            path.TargetInfo.Rotation = MapOrientation(snapshotDisplay.Orientation, currentPath.PathInfo.TargetInfo.Rotation);
            path.TargetInfo.RefreshRate = ResolveRefreshRate(snapshotDisplay, currentPath);
            path.TargetInfo.TargetAvailable = true;
            path.TargetInfo.StatusFlags = 0;

            targetPaths.Add(path);
        }

        paths = targetPaths.ToArray();
        modes = displayModes.ToArray();
        message = string.Empty;
        return true;
    }

    private static bool TrySelectUniqueSourceCandidates(
        IReadOnlyList<(DisplayPathSnapshot Snapshot, IReadOnlyList<NativeDisplayPath> Candidates)> candidateSets,
        out List<(DisplayPathSnapshot Snapshot, NativeDisplayPath Current)> matched)
    {
        var selected = new List<(DisplayPathSnapshot Snapshot, NativeDisplayPath Current)>();
        matched = selected;
        var usedSources = new HashSet<string>(StringComparer.Ordinal);
        var orderedSets = candidateSets
            .OrderBy(set => set.Candidates.Count)
            .ToList();

        bool Select(int index)
        {
            if (index == orderedSets.Count)
            {
                return true;
            }

            var set = orderedSets[index];
            foreach (var candidate in set.Candidates
                .OrderByDescending(candidate => ScoreCandidate(set.Snapshot, candidate)))
            {
                var sourceKey = $"{candidate.PathInfo.SourceInfo.AdapterId.ToInt64()}:{candidate.PathInfo.SourceInfo.Id}";
                if (!usedSources.Add(sourceKey))
                {
                    continue;
                }

                selected.Add((set.Snapshot, candidate));
                if (Select(index + 1))
                {
                    return true;
                }

                selected.RemoveAt(selected.Count - 1);
                usedSources.Remove(sourceKey);
            }

            return false;
        }

        return Select(0);
    }

    private static NativeDisplayPath? SelectBestCandidate(DisplayPathSnapshot snapshotDisplay, IReadOnlyList<NativeDisplayPath> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var ranked = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = ScoreCandidate(snapshotDisplay, candidate)
            })
            .OrderByDescending(item => item.Score)
            .ToList();

        return ranked.Count == 1 || ranked[0].Score > ranked[1].Score
            ? ranked[0].Candidate
            : null;
    }

    private static int ScoreCandidate(DisplayPathSnapshot snapshotDisplay, NativeDisplayPath candidate)
    {
        var score = 0;
        if (candidate.IsActive)
        {
            score += 100;
        }

        if (candidate.IsAvailable)
        {
            score += 25;
        }

        if (snapshotDisplay.AdapterLuid == candidate.AdapterLuid)
        {
            score += 20;
        }

        if (snapshotDisplay.TargetId == candidate.TargetId)
        {
            score += 25;
        }

        if (snapshotDisplay.Width == candidate.Width && snapshotDisplay.Height == candidate.Height)
        {
            score += 5;
        }

        // SourceId for inactive displays is often stale or remapped by Windows.
        // Keep it as a weak tiebreaker only and do not let it dominate ranking.
        if (snapshotDisplay.SourceId == candidate.SourceId)
        {
            score += 2;
        }

        if (snapshotDisplay.RefreshRateNumerator == 0 || snapshotDisplay.RefreshRateDenominator == 0)
        {
            score += Math.Min(50, (int)Math.Round(candidate.RefreshRateHz() / 3));
        }

        if (candidate.TargetMode is not null)
        {
            score += 20;
        }

        return score;
    }

    private static DisplayConfigTargetMode BuildTargetMode(
        DisplayPathSnapshot snapshotDisplay,
        NativeDisplayPath currentPath,
        int width,
        int height)
    {
        var targetMode = currentPath.TargetMode ?? new DisplayConfigTargetMode();
        var signalInfo = targetMode.TargetVideoSignalInfo;

        signalInfo.ActiveSize = new DisplayConfig2DRegion
        {
            Cx = checked((uint)width),
            Cy = checked((uint)height)
        };

        if (signalInfo.TotalSize.Cx == 0 || signalInfo.TotalSize.Cy == 0)
        {
            signalInfo.TotalSize = signalInfo.ActiveSize;
        }

        var refreshRate = ResolveRefreshRate(snapshotDisplay, currentPath);
        if (refreshRate.Numerator > 0 && refreshRate.Denominator > 0)
        {
            signalInfo.VSyncFreq = refreshRate;
        }

        if (signalInfo.ScanLineOrdering == 0)
        {
            signalInfo.ScanLineOrdering = currentPath.PathInfo.TargetInfo.ScanLineOrdering;
        }

        targetMode.TargetVideoSignalInfo = signalInfo;
        return targetMode;
    }

    private static DisplayConfigRational ResolveRefreshRate(DisplayPathSnapshot snapshotDisplay, NativeDisplayPath currentPath)
    {
        if (snapshotDisplay.RefreshRateNumerator > 0 && snapshotDisplay.RefreshRateDenominator > 0)
        {
            return new DisplayConfigRational
            {
                Numerator = snapshotDisplay.RefreshRateNumerator,
                Denominator = snapshotDisplay.RefreshRateDenominator
            };
        }

        if (currentPath.PathInfo.TargetInfo.RefreshRate.Numerator > 0
            && currentPath.PathInfo.TargetInfo.RefreshRate.Denominator > 0)
        {
            return currentPath.PathInfo.TargetInfo.RefreshRate;
        }

        return currentPath.TargetMode?.TargetVideoSignalInfo.VSyncFreq ?? new DisplayConfigRational();
    }

    private static int ResolveWidth(DisplayPathSnapshot snapshotDisplay, NativeDisplayPath currentPath)
    {
        if (snapshotDisplay.Width > 0)
        {
            return snapshotDisplay.Width;
        }

        if (currentPath.Width > 0)
        {
            return currentPath.Width;
        }

        return currentPath.TargetMode is { } targetMode ? checked((int)targetMode.TargetVideoSignalInfo.ActiveSize.Cx) : 0;
    }

    private static int ResolveHeight(DisplayPathSnapshot snapshotDisplay, NativeDisplayPath currentPath)
    {
        if (snapshotDisplay.Height > 0)
        {
            return snapshotDisplay.Height;
        }

        if (currentPath.Height > 0)
        {
            return currentPath.Height;
        }

        return currentPath.TargetMode is { } targetMode ? checked((int)targetMode.TargetVideoSignalInfo.ActiveSize.Cy) : 0;
    }

    private static DisplayIdentity ToIdentity(NativeDisplayPath path)
    {
        return new DisplayIdentity
        {
            MonitorDevicePath = path.MonitorDevicePath,
            FriendlyName = path.MonitorFriendlyName,
            EdidIdsValid = path.HasEdid,
            EdidManufacturerId = path.EdidManufacturerId,
            EdidProductCodeId = path.EdidProductCodeId,
            ConnectorInstance = path.ConnectorInstance,
            OutputTechnology = path.ConnectionTechnology,
            AdapterLuid = path.AdapterLuid,
            TargetId = path.TargetId
        };
    }

    private static DisplayConfigRotation MapOrientation(DisplayOrientation orientation, DisplayConfigRotation fallback)
    {
        return orientation switch
        {
            DisplayOrientation.Identity => DisplayConfigRotation.Identity,
            DisplayOrientation.Rotate90 => DisplayConfigRotation.Rotate90,
            DisplayOrientation.Rotate180 => DisplayConfigRotation.Rotate180,
            DisplayOrientation.Rotate270 => DisplayConfigRotation.Rotate270,
            _ => fallback
        };
    }

    private NativeDisplayTopology QueryActiveTopology(CancellationToken cancellationToken)
    {
        return QueryTopology(DisplayConfigNative.QdcOnlyActivePaths, "active", cancellationToken);
    }

    private NativeDisplayTopology QueryAvailableTopology(CancellationToken cancellationToken)
    {
        return QueryTopology(DisplayConfigNative.QdcAllPaths, "available", cancellationToken);
    }

    private NativeDisplayTopology QueryLayoutApplicationTopology(CancellationToken cancellationToken)
    {
        try
        {
            var database = QueryTopology(DisplayConfigNative.QdcDatabaseCurrent, "database", cancellationToken);
            var available = QueryAvailableTopology(cancellationToken);
            return MergeTopologies(database, available);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DisplayConfig database topology could not be read. Falling back to all paths.");
            return QueryAvailableTopology(cancellationToken);
        }
    }

    private static NativeDisplayTopology MergeTopologies(NativeDisplayTopology primary, NativeDisplayTopology secondary)
    {
        var paths = primary.Paths
            .Concat(secondary.Paths)
            .GroupBy(path => $"{path.PathInfo.SourceInfo.AdapterId.ToInt64()}:{path.SourceId}:{path.PathInfo.TargetInfo.AdapterId.ToInt64()}:{path.TargetId}")
            .Select(group => group
                .OrderByDescending(path => path.IsActive)
                .ThenByDescending(path => path.IsAvailable)
                .ThenByDescending(path => path.TargetMode is not null)
                .ThenByDescending(GetTargetModeArea)
                .ThenByDescending(path => path.Width * path.Height)
                .First())
            .ToList();

        return new NativeDisplayTopology(paths, primary.PrimarySourceDeviceName, primary.CapturedAt);
    }

    private NativeDisplayTopology QueryTopology(uint queryFlags, string description, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reading {Description} display configuration through Windows DisplayConfig.", description);

        for (var attempt = 1; attempt <= MaxQueryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sizeResult = DisplayConfigNative.GetDisplayConfigBufferSizes(
                queryFlags,
                out var pathCount,
                out var modeCount);

            DisplayConfigNative.ThrowIfWin32Error(sizeResult, nameof(DisplayConfigNative.GetDisplayConfigBufferSizes));
            _logger.LogDebug("DisplayConfig buffer sizes. Paths: {PathCount}, Modes: {ModeCount}, Attempt: {Attempt}.", pathCount, modeCount, attempt);

            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];

            var queryPathCount = pathCount;
            var queryModeCount = modeCount;
            var queryResult = DisplayConfigNative.QueryDisplayConfig(
                queryFlags,
                ref queryPathCount,
                paths,
                ref queryModeCount,
                modes,
                IntPtr.Zero);

            if (queryResult == DisplayConfigNative.ErrorInsufficientBuffer)
            {
                _logger.LogWarning("Display configuration changed while reading buffers. Retrying DisplayConfig query. Attempt: {Attempt}.", attempt);
                continue;
            }

            if (queryResult != DisplayConfigNative.ErrorSuccess)
            {
                _logger.LogError("QueryDisplayConfig failed with Win32 error {ErrorCode}. Attempt: {Attempt}.", queryResult, attempt);
                DisplayConfigNative.ThrowIfWin32Error(queryResult, nameof(DisplayConfigNative.QueryDisplayConfig));
            }

            var queriedPaths = paths.Take((int)queryPathCount).ToArray();
            var queriedModes = modes.Take((int)queryModeCount).ToArray();
            _logger.LogInformation("QueryDisplayConfig returned {PathCount} {Description} path(s).", queriedPaths.Length, description);

            return new NativeDisplayTopology(
                queriedPaths.Select(path => MapNativePath(path, queriedModes)).ToList(),
                GetPrimarySourceDeviceName(),
                DateTimeOffset.UtcNow);
        }

        throw new InvalidOperationException("Display configuration changed during each DisplayConfig query attempt.");
    }

    private NativeDisplayPath MapNativePath(DisplayConfigPathInfo path, IReadOnlyList<DisplayConfigModeInfo> modes)
    {
        var sourceMode = ResolveSourceMode(path, modes);
        var targetMode = ResolveTargetMode(path, modes);
        var sourceName = TryGetSourceName(path.SourceInfo.AdapterId, path.SourceInfo.Id);
        var targetName = GetTargetName(path.TargetInfo.AdapterId, path.TargetInfo.Id);
        var preferredMode = TryGetTargetPreferredMode(path.TargetInfo.AdapterId, path.TargetInfo.Id);
        var effectiveTargetMode = targetMode ?? preferredMode?.TargetMode;
        var hasEdid = targetName.Flags.EdidIdsValid
            && targetName.EdidManufactureId != 0
            && targetName.EdidProductCodeId != 0;
        var isActive = (path.Flags & DisplayConfigNative.DisplayConfigPathActive) == DisplayConfigNative.DisplayConfigPathActive;
        var width = ResolveNativeWidth(isActive, sourceMode, effectiveTargetMode, preferredMode?.Width ?? 0);
        var height = ResolveNativeHeight(isActive, sourceMode, effectiveTargetMode, preferredMode?.Height ?? 0);

        if (!hasEdid)
        {
            _logger.LogDebug(
                "Display target does not expose complete EDID data. Source: {SourceDeviceName}, TargetId: {TargetId}, ConnectorInstance: {ConnectorInstance}.",
                sourceName,
                path.TargetInfo.Id,
                targetName.ConnectorInstance);
        }

        _logger.LogDebug(
            "DisplayConfig path mapped. Active: {IsActive}, Available: {IsAvailable}, Source: {SourceDeviceName}, FriendlyName: {FriendlyName}, Connection: {ConnectionTechnology}.",
            isActive,
            path.TargetInfo.TargetAvailable,
            sourceName,
            string.IsNullOrWhiteSpace(targetName.MonitorFriendlyDeviceName) ? "(unnamed)" : targetName.MonitorFriendlyDeviceName,
            FormatOutputTechnology(path.TargetInfo.OutputTechnology));

        return new NativeDisplayPath
        {
            SourceDeviceName = sourceName,
            MonitorFriendlyName = targetName.MonitorFriendlyDeviceName,
            MonitorDevicePath = targetName.MonitorDevicePath,
            HasEdid = hasEdid,
            EdidManufacturerId = targetName.EdidManufactureId,
            EdidProductCodeId = targetName.EdidProductCodeId,
            ConnectorInstance = targetName.ConnectorInstance,
            AdapterLuid = path.SourceInfo.AdapterId.ToInt64(),
            SourceId = path.SourceInfo.Id,
            TargetId = path.TargetInfo.Id,
            PositionX = sourceMode?.Position.X ?? 0,
            PositionY = sourceMode?.Position.Y ?? 0,
            Width = checked((int)width),
            Height = checked((int)height),
            Rotation = MapRotation(path.TargetInfo.Rotation),
            RefreshRateNumerator = ResolveRefreshRate(path.TargetInfo.RefreshRate, effectiveTargetMode).Numerator,
            RefreshRateDenominator = ResolveRefreshRate(path.TargetInfo.RefreshRate, effectiveTargetMode).Denominator,
            ConnectionTechnology = FormatOutputTechnology(path.TargetInfo.OutputTechnology),
            IsActive = isActive,
            IsAvailable = path.TargetInfo.TargetAvailable,
            PathInfo = path,
            SourceMode = sourceMode ?? new DisplayConfigSourceMode
            {
                Width = effectiveTargetMode?.TargetVideoSignalInfo.ActiveSize.Cx ?? preferredMode?.Width ?? 0,
                Height = effectiveTargetMode?.TargetVideoSignalInfo.ActiveSize.Cy ?? preferredMode?.Height ?? 0,
                PixelFormat = DisplayConfigPixelFormat.BitsPerPixel32,
                Position = new PointL()
            },
            TargetMode = effectiveTargetMode
        };
    }

    private static DisplayConfigRational ResolveRefreshRate(DisplayConfigRational pathRefreshRate, DisplayConfigTargetMode? targetMode)
    {
        if (pathRefreshRate.Numerator > 0 && pathRefreshRate.Denominator > 0)
        {
            return pathRefreshRate;
        }

        var targetRefreshRate = targetMode?.TargetVideoSignalInfo.VSyncFreq ?? default;
        return targetRefreshRate.Numerator > 0 && targetRefreshRate.Denominator > 0
            ? targetRefreshRate
            : default;
    }

    private static string FormatModes(IReadOnlyList<DisplayModeInfo> modes)
    {
        return $"[{string.Join(", ", modes.Select(mode => FormatMode(mode.Width, mode.Height, mode.RefreshRateHz)))}]";
    }

    private static string FormatMode(int width, int height, double refreshRate)
    {
        return width > 0 && height > 0 && refreshRate > 0
            ? $"{width}x{height}@{refreshRate:0.##}"
            : "unknown";
    }

    private static uint ResolveNativeWidth(
        bool isActive,
        DisplayConfigSourceMode? sourceMode,
        DisplayConfigTargetMode? targetMode,
        uint preferredWidth)
    {
        var sourceWidth = sourceMode?.Width ?? 0;
        var targetWidth = targetMode?.TargetVideoSignalInfo.ActiveSize.Cx ?? 0;
        var inactiveWidth = preferredWidth > 0 ? preferredWidth : targetWidth;
        return !isActive && inactiveWidth > 0
            ? inactiveWidth
            : sourceWidth > 0 ? sourceWidth : targetWidth;
    }

    private static uint ResolveNativeHeight(
        bool isActive,
        DisplayConfigSourceMode? sourceMode,
        DisplayConfigTargetMode? targetMode,
        uint preferredHeight)
    {
        var sourceHeight = sourceMode?.Height ?? 0;
        var targetHeight = targetMode?.TargetVideoSignalInfo.ActiveSize.Cy ?? 0;
        var inactiveHeight = preferredHeight > 0 ? preferredHeight : targetHeight;
        return !isActive && inactiveHeight > 0
            ? inactiveHeight
            : sourceHeight > 0 ? sourceHeight : targetHeight;
    }

    private static ulong GetTargetModeArea(NativeDisplayPath path)
    {
        if (path.TargetMode is not { } targetMode)
        {
            return 0;
        }

        return (ulong)targetMode.TargetVideoSignalInfo.ActiveSize.Cx
            * targetMode.TargetVideoSignalInfo.ActiveSize.Cy;
    }

    private static DisplayConfigSourceMode? ResolveSourceMode(DisplayConfigPathInfo path, IReadOnlyList<DisplayConfigModeInfo> modes)
    {
        if (path.SourceInfo.ModeInfoIdx < modes.Count)
        {
            var mode = modes[(int)path.SourceInfo.ModeInfoIdx];
            if (mode.InfoType == DisplayConfigModeInfoType.Source)
            {
                return mode.ModeInfo.SourceMode;
            }
        }

        var fallback = modes.FirstOrDefault(mode =>
            mode.InfoType == DisplayConfigModeInfoType.Source
            && mode.Id == path.SourceInfo.Id
            && mode.AdapterId.ToInt64() == path.SourceInfo.AdapterId.ToInt64());

        return fallback.InfoType == DisplayConfigModeInfoType.Source
            ? fallback.ModeInfo.SourceMode
            : null;
    }

    private static DisplayConfigTargetMode? ResolveTargetMode(DisplayConfigPathInfo path, IReadOnlyList<DisplayConfigModeInfo> modes)
    {
        if (path.TargetInfo.ModeInfoIdx < modes.Count)
        {
            var mode = modes[(int)path.TargetInfo.ModeInfoIdx];
            if (mode.InfoType == DisplayConfigModeInfoType.Target)
            {
                return mode.ModeInfo.TargetMode;
            }
        }

        var fallback = modes.FirstOrDefault(mode =>
            mode.InfoType == DisplayConfigModeInfoType.Target
            && mode.Id == path.TargetInfo.Id
            && mode.AdapterId.ToInt64() == path.TargetInfo.AdapterId.ToInt64());

        return fallback.InfoType == DisplayConfigModeInfoType.Target
            ? fallback.ModeInfo.TargetMode
            : null;
    }

    private static string TryGetSourceName(Luid adapterId, uint sourceId)
    {
        var request = new DisplayConfigSourceDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfigDeviceInfoType.GetSourceName,
                Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                AdapterId = adapterId,
                Id = sourceId
            },
            ViewGdiDeviceName = string.Empty
        };

        var result = DisplayConfigNative.DisplayConfigGetDeviceInfo(ref request);
        return result == DisplayConfigNative.ErrorSuccess
            ? request.ViewGdiDeviceName
            : string.Empty;
    }

    private static DisplayConfigTargetDeviceName GetTargetName(Luid adapterId, uint targetId)
    {
        var request = new DisplayConfigTargetDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfigDeviceInfoType.GetTargetName,
                Size = (uint)Marshal.SizeOf<DisplayConfigTargetDeviceName>(),
                AdapterId = adapterId,
                Id = targetId
            },
            MonitorFriendlyDeviceName = string.Empty,
            MonitorDevicePath = string.Empty
        };

        var result = DisplayConfigNative.DisplayConfigGetDeviceInfo(ref request);
        DisplayConfigNative.ThrowIfWin32Error(result, "DisplayConfigGetDeviceInfo(TargetName)");
        return request;
    }

    private DisplayConfigTargetPreferredMode? TryGetTargetPreferredMode(Luid adapterId, uint targetId)
    {
        var request = new DisplayConfigTargetPreferredMode
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfigDeviceInfoType.GetTargetPreferredMode,
                Size = (uint)Marshal.SizeOf<DisplayConfigTargetPreferredMode>(),
                AdapterId = adapterId,
                Id = targetId
            }
        };

        var result = DisplayConfigNative.DisplayConfigGetDeviceInfo(ref request);
        if (result != DisplayConfigNative.ErrorSuccess)
        {
            _logger.LogDebug(
                "DisplayConfigGetDeviceInfo(TargetPreferredMode) failed with Win32 error {ErrorCode}. TargetId: {TargetId}.",
                result,
                targetId);
            return null;
        }

        return request.Width > 0 && request.Height > 0
            ? request
            : null;
    }

    private static NativeDisplayRotation MapRotation(DisplayConfigRotation rotation)
    {
        return rotation switch
        {
            DisplayConfigRotation.Identity => NativeDisplayRotation.Identity,
            DisplayConfigRotation.Rotate90 => NativeDisplayRotation.Rotate90,
            DisplayConfigRotation.Rotate180 => NativeDisplayRotation.Rotate180,
            DisplayConfigRotation.Rotate270 => NativeDisplayRotation.Rotate270,
            _ => NativeDisplayRotation.Unknown
        };
    }

    private static string FormatOutputTechnology(DisplayConfigVideoOutputTechnology technology)
    {
        return technology switch
        {
            DisplayConfigVideoOutputTechnology.Hd15 => "VGA",
            DisplayConfigVideoOutputTechnology.Dvi => "DVI",
            DisplayConfigVideoOutputTechnology.Hdmi => "HDMI",
            DisplayConfigVideoOutputTechnology.DisplayPortExternal => "DisplayPort",
            DisplayConfigVideoOutputTechnology.DisplayPortEmbedded => "Embedded DisplayPort",
            DisplayConfigVideoOutputTechnology.Internal => "Internal",
            DisplayConfigVideoOutputTechnology.Miracast => "Miracast",
            DisplayConfigVideoOutputTechnology.IndirectWired => "Indirect wired",
            DisplayConfigVideoOutputTechnology.Lvds => "LVDS",
            DisplayConfigVideoOutputTechnology.Other => "Other",
            _ => technology.ToString()
        };
    }

    private string GetPrimarySourceDeviceName()
    {
        for (uint index = 0; ; index++)
        {
            var displayDevice = new DisplayDevice
            {
                Cb = (uint)Marshal.SizeOf<DisplayDevice>(),
                DeviceName = string.Empty,
                DeviceString = string.Empty,
                DeviceId = string.Empty,
                DeviceKey = string.Empty
            };

            if (!DisplayConfigNative.EnumDisplayDevices(null, index, ref displayDevice, 0))
            {
                break;
            }

            if ((displayDevice.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) == DisplayDeviceStateFlags.PrimaryDevice)
            {
                _logger.LogDebug("EnumDisplayDevices identified primary source device {SourceDeviceName}.", displayDevice.DeviceName);
                return displayDevice.DeviceName;
            }
        }

        _logger.LogWarning("EnumDisplayDevices did not identify a primary display source.");
        return string.Empty;
    }
}

# GameShell architecture audit

Audit date: 2026-08-13

This document is the baseline for the staged refactor. It describes the current
structure before the refactor and separates confirmed architectural problems from
intentional compatibility code.

## Current boundaries

- `GameShell.Core` contains models, contracts, settings/runtime persistence,
  display policies and mode orchestration. It does not reference Windows or WPF.
- `GameShell.Windows` contains DisplayConfig/Win32, process and launcher
  integration. Audio switching is currently not part of the application surface;
  the former audio adapter and settings contract were removed until the feature
  has a real product use case. Windows default audio behavior remains untouched.
- `GameShell.Desktop` is the WPF composition root, tray/single-instance host,
  SDL controller host and WebView2 bridge. It references Core and Windows.
- `GameShell.Ui` is the React/TypeScript presentation layer. It communicates with
  Desktop through the WebView bridge and keeps UI draft state separate from the
  persisted settings payload.

The dependency direction is acceptable. The main issue is that several classes
inside those boundaries contain multiple use-case responsibilities.

## Launcher elevation slice

The launcher start audit found two process-start call sites: the gaming workflow
and the launcher recovery restart path. Both previously called `Process.Start`
directly from `GameShell.Windows`, which made an elevated GameShell pass its
elevated token to Playnite.

The shared boundary is now `IExternalProcessLauncher` plus
`ILauncherProcessService` in Core. `LauncherProcessRequestFactory` keeps the
launcher mapping and arguments policy in Core, while
`WindowsExternalProcessLauncher` owns the interactive standard-user token,
environment creation and Win32 process creation. Profile activation and
recovery use the same `StartStandardUser` path. A failed standard-user start is
returned as a controlled workflow/recovery failure; there is no elevated silent
fallback.

## Highest-priority classes

The sizes below are the audit baseline before the staged refactor.

| Class | Size | Confirmed responsibilities | Risk |
|---|---:|---|---|
| `WindowsDisplayConfigurationService` | 1249 lines | Display inventory, DisplayConfig query, identity mapping, mode enumeration, topology validation, native topology construction, apply/verify and GDI refresh correction | High: native behavior is stable and must be characterized before extraction |
| `WebViewBridge` | 1154 lines | WebView transport, command allow-list/routing, JSON payload parsing, settings/profile mutations, display operations, dependency dialogs, controller capture and status notifications | High: many unrelated commands share one failure boundary |
| `NativeModeWorkflowService` | 888 lines | Desktop shell visibility, window management, taskbar/icons, cursor state, launcher process control, audio and workflow step policy | High: OS side effects and rollback/recovery behavior |
| `App` | 864 lines | Composition root, single-instance signaling, tray menu, window lifetime, global hotkeys, startup commands and shutdown | High: WPF lifecycle makes direct unit testing difficult |
| `ModeOrchestrator` | 808 lines | Mode use cases, operation locking, readiness gates, display transition, workflow execution, runtime persistence, status state and profile resolution | High: central behavior, but a pure profile-resolution slice is low risk |
| `SdlControllerShortcutService` | 703 lines | SDL lifecycle, polling loop, capture state machine, shortcut matching, status snapshots and rate-limited diagnostics | Medium/high: polling is stateful; extract pure matching/diagnostic policies first |
| `WindowsLauncherRecoveryStrategy` | 596 lines | Process discovery, launcher start/close, diagnostics, focus inspection and Win32 focus recovery | Medium: can split behind narrow OS adapters after characterization tests |

## Specific design problems

### Orchestration and policy are mixed

`ModeOrchestrator` contains the transition pipeline and also resolves the active
gaming profile, effective launcher/behaviour settings and legacy layout fallback.
The profile resolver is deterministic and has no I/O, so it is a clear first
extraction target. The transition pipeline itself should be split only after its
current result/status semantics are covered by tests.

### Transport and application use cases are mixed

`WebViewBridge.OnWebMessageReceived` owns deserialization, command dispatch,
logging, error conversion and status notification. Its large switch hides the
actual application operations and makes command-level tests require WebView2.
The next safe step is to introduce command handlers/application services while
keeping the existing bridge response contract unchanged.

### Native display query and native display apply are mixed

`WindowsDisplayConfigurationService` combines query snapshots, mode inventory,
identity conversion, topology matching and `SetDisplayConfig`. These are related
but separate reasons to change. No extraction should alter flags, topology math,
or native ABI. First add characterization tests around mapping/selection, then
extract query/mapping and apply orchestration behind existing interfaces.

### Desktop host owns too much mutable lifecycle state

`App` has independent state machines for tray, hotkeys, command events and
shutdown. It also repeats mode-command wrappers for tray, startup and hotkeys.
The composition root should construct small host services; WPF callbacks should
only forward events and manage lifetime.

### Windows workflow has an overly broad OS façade

`NativeModeWorkflowService` contains several independent adapters (desktop shell,
window operations, cursor, taskbar/icons and launcher operations) plus workflow
policy and warning aggregation. The first extraction should be pure workflow
step/result aggregation; P/Invoke movement comes later with runtime tests.

### Testability gaps

- Core pure policies are testable, but some are private static methods inside
  orchestrators instead of public small services.
- WebView tests would need a concrete `CoreWebView2` because transport and use
  cases are coupled.
- `App` uses `Application.Current`, WPF dispatcher, Win32 hotkeys and named
  kernel events directly, so lifecycle logic is not unit-test friendly.
- Windows classes call static P/Invoke wrappers directly. This is appropriate at
  the infrastructure edge, but the orchestration around them should depend on
  narrow interfaces.

## Redundancy and compatibility findings

- Profile selection/effective behaviour resolution is duplicated across
  `ModeOrchestrator`, Desktop hotkey handling and UI profile helpers. The Core
  resolver should become the source of truth for runtime profile selection.
- `WebViewBridge` contains several command-specific payload readers and save
  paths that should converge on typed request handlers, but the existing JSON
  contract must remain stable.
- `--gaming`, `--pcgaming` and `pcGaming*` settings names are intentional
  compatibility inputs. They must not be removed as dead code until a migration
  policy explicitly allows it.
- No dead code is removed in the audit phase without a call-site proof. Generated
  bundles and installer output remain build artifacts, not alternative source
  implementations.

## Staged refactor plan

1. Extract `GamingProfileRuntimeSettingsResolver` from `ModeOrchestrator` and
   add direct unit tests for requested/active/fallback profile resolution,
   topology conversion, legacy behaviour fallback and effective settings.
2. Characterize and simplify the Core transition result pipeline. Extract only
   result creation/status persistence if tests demonstrate a stable seam.
3. Split WebView command routing from typed application handlers without changing
   command names or response JSON.
4. Split Desktop host services: `TrayHost`, `GlobalHotkeyHost` and
   `SingleInstanceCommandHost`; keep `App` as composition/lifetime coordinator.
5. Split Windows display query/mapping/apply only after native characterization
   tests pass. Preserve all CCD flags, ABI structures and topology calculations.
6. Split native workflow and SDL polling around narrow OS adapters and pure
   policies; verify each slice with tests and bounded manual runtime checks.

Each slice must pass the existing build/tests and add focused tests before the
next slice begins. The manual display/launcher/controller runtime behavior remains
the definition of done for infrastructure changes.

## Completed in this iteration

- `GamingProfileRuntimeSettingsResolver` extracted profile selection, topology
  conversion and effective launcher/behaviour resolution from `ModeOrchestrator`.
  Desktop hotkey selection now uses the same Core policy instead of duplicating it.
- `GamingProfileReadinessCoordinator` extracted readiness gates, reason messages
  and last-known-good capability updates. `ModeOrchestrator` now
  owns the transition pipeline and status, while the coordinator owns readiness.
- `WebViewCommandRouter` extracted command allow-list and dispatch from the WebView2
  transport boundary. Command names and response JSON are unchanged.
- `LegacySettingsPropertyMigrator` extracted old `pcGaming*` JSON compatibility from
  the file/cache/backup persistence service.
- `GlobalHotkeyParser` extracted side-effect-free hotkey parsing from the WPF app
  lifecycle class.
- `WindowsIntegrationService` extracted registry/startup/shortcut side effects from
  `WebViewBridge`; `WindowsCursorService` extracted cursor P/Invoke from the mode
  workflow. Both remain in Desktop/Windows infrastructure respectively.
- `WindowsDisplayTopologyReader` extracted DisplayConfig query/retry, native path
  mapping, target/source metadata lookup and topology merging from
  `WindowsDisplayConfigurationService`. The existing configuration service still
  owns validation, candidate selection and `SetDisplayConfig` application.
- `WebViewPayloadReader` extracted stateless JSON payload validation and model
  deserialization from `WebViewBridge`. The bridge response serialization and
  command contract remain unchanged.
- `WebViewDisplayCommandHandler` extracted display inventory, layout capture,
  profile layout mutation, display test and display selection logic from
  `WebViewBridge`. The bridge now remains a transport/orchestration boundary;
  display command behavior and JSON command names are unchanged.
- `ShortcutConflictNormalizer` extracted profile keyboard/controller shortcut
  normalization and conflict cleanup from `AppSettingsValidator`.
- `ControllerShortcutMatcher` extracted pure controller combination matching,
  hold-duration policy and trigger state from the SDL polling service. Focused
  tests cover exact combinations, Work Mode and retriggering after release.
- Audio handling was intentionally removed from the current product surface:
  `IAudioDeviceService`, `WindowsAudioDeviceService`, audio settings persistence,
  WebView audio commands and UI audio DTOs are gone. Existing unknown audio JSON
  fields are ignored by deserialization; no audio device is changed by runtime.
- `SingleInstanceCommandHost` extracted named-event registration, WPF dispatcher
  marshalling and cleanup from `App`. `App` remains responsible for choosing the
  commands and preserving startup/shutdown policy.
- `GlobalHotkeyHost` extracted Win32 hotkey registration, profile hotkey mapping,
  capture-state reload and cleanup from `App`. `App` now only composes the host
  with the existing tray command runner and WebView capture event.
- `TrayHost` extracted tray icon lifetime, menu rendering, tray state refresh and
  menu callbacks from `App`. Tray actions still use the same mode command service
  and shutdown policy; `App` remains the WPF lifecycle coordinator.
- Static cleanup completed: `dotnet format style --diagnostics IDE0005` reports no
  unused `using` directives. One confirmed unreachable private helper,
  `SelectBestCandidate` in `WindowsDisplayConfigurationService`, was removed.
  Public/DI/bridge entry points were retained unless a call-site audit proved
  they were unused.
- P/Invoke review completed: `DllImport` remains intentional for the current
  native boundary. `DisplayConfigNative` contains ABI-sensitive DisplayConfig
  structures, fixed native text buffers and variable-sized path/mode arrays;
  replacing it mechanically with `LibraryImport` could alter marshalling or
  structure semantics. The analyzer recommendation is an optional optimization,
  not a correctness defect. Simple Win32 calls may be migrated later in an
  isolated slice with ABI and runtime tests.
- `IWindowsWindowManagementService` and `WindowsWindowManagementService` extracted
  desktop icons, taskbars, Explorer/window cleanup, self-minimization and launcher
  foreground Win32 operations from `NativeModeWorkflowService`. The workflow still
  owns step ordering, cancellation, warning aggregation and launcher process policy.
- Focused tests were added for profile resolution, WebView routing, legacy settings
  migration, hotkey parsing and controller shortcut matching. Current .NET test
  result: `191/191` passed.

## Current class-size checkpoint

No production C# class currently exceeds 1000 lines. The largest remaining files
are `WindowsDisplayConfigurationService` (871), `SdlControllerShortcutService`
(655), `WindowsLauncherRecoveryStrategy` (596), `NativeModeWorkflowService`
(503), `DisplayConfigNative` (489), `WebViewBridge` (485),
`DisplayTransitionService` (464), `WebViewDisplayCommandHandler` (449),
`TrayHost` (213) and `App` (459). They remain candidates for focused slices,
but must be split by responsibility and not by arbitrary line boundaries.

The remaining architectural work is therefore primarily about reducing the
remaining lifecycle coupling in `App`, separating workflow policy from OS
effects, and extracting typed WebView application handlers. Native DisplayConfig
apply logic remains in place deliberately because its CCD flags, ABI and topology
calculations are stable runtime-sensitive behavior.

## Refactor non-goals

- No product behavior changes.
- No friendly-name aliases or Samsung-specific rules.
- No changes to `SetDisplayConfig` flags, CCD ABI, topology math or refresh-policy
  semantics.
- No big-bang namespace/project rewrite and no mechanical class splitting solely
  to reduce line count.

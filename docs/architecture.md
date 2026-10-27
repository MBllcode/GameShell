# Architecture

[← Back to README](../README.md)

GameShell separates desktop presentation, mode/session decisions, and Windows side effects. The UI describes the desired profile; native services apply it.

```mermaid
flowchart LR
    UI[React UI / WebView2] --> Commands[Mode command service]
    Tray[Tray / keyboard / controller] --> Commands
    API[Optional LAN API] --> Commands
    Commands --> Sessions[Session manager]
    Sessions --> Modes[Mode orchestrator]
    Modes --> Displays[Display transition service]
    Modes --> Workflow[Windows desktop / launcher workflow]
    Displays --> Native[Windows DisplayConfig]
```

## Project boundaries

| Project | Main responsibilities | Depends on |
| --- | --- | --- |
| `GameShell.Core` | Models, state machines, validation, settings, transitions, service interfaces | Logging abstractions |
| `GameShell.Windows` | DisplayConfig interop, mode enumeration, process/session monitoring, window/cursor operations | Core |
| `GameShell.Desktop` | WPF lifecycle, dependency injection, WebView bridge, tray, hotkeys, SDL controller input, HTTP host | Core, Windows |
| `GameShell.Ui` | React profile/setup flows, form drafts, layout preview, localization | Typed WebView message contract |

`tests/GameShell.Core.Tests` contains xUnit tests across the core, Windows mapping/geometry, and desktop routing/security boundaries. UI tests use Node's built-in test runner for routing, display identity/topology, shortcuts, localization, and status mapping.

## A profile transition

1. UI, tray, shortcut, or API resolves a command through `IModeCommandService`.
2. The session manager serializes lifecycle work and resolves the profile's own launcher, behaviour, and shortcuts.
3. Display discovery waits for stable inventory. The transition service applies the requested topology and verifies it.
4. The Windows workflow performs enabled desktop/cursor/window actions and starts the launcher.
5. Session monitoring tracks game/launcher activity. Work Mode runs the restoration path; transition failures can use the captured rollback layout.

Startup resolves one target from startup preferences and committed runtime state. Work Mode reconciliation also restores desktop behaviour when the display topology already matches; matching monitors alone does not imply that the taskbar, cursor, or launcher state is correct.

## Display identity

Known physical displays are stored separately from transient Windows observations and profile-local settings. A route, adapter identifier, friendly name, or mode-list change does not by itself mean a new physical monitor.

Historical capability data is diagnostic. `AutoHighest` uses the active modes currently exposed by Windows after activation. `Fixed` remains strict. Normal transitions do not reset GPUs, monitor devices, HDMI inputs, or TV power.

## Persistence and state

- `%LOCALAPPDATA%\GameShell\settings.json` stores configuration. Callers receive independent snapshots; the cache is published only after a successful atomic save. A backup supports recovery.
- `state.json` stores committed runtime targets and return/previous layouts. Writes are serialized and use temporary-file replacement plus backup fallback.
- Backend-owned transient operation state drives the bottom status line. Transition IDs prevent an older completion from clearing newer work.
- Session generations reject late game/process callbacks from an earlier session.

## Host and UI

The manifest declares PerMonitorV2 before any WPF, tray, or WebView HWND is created. Saved window placement is checked against active monitor work areas after topology changes.

The footer has a dedicated layout row. `ViewportContent` measures the remaining page region and fits content when necessary, keeping the whole page visible without a window scrollbar. Display-canvas drag coordinates account for the resulting scale; the display inspector retains its own bounded scroll region.

The WebView bridge accepts privileged messages only from `https://app.gameshell.local` on the default HTTPS port, blocks navigation to other origins, and suppresses popup windows. `about:blank` is allowed only for the missing-assets page and is not a trusted command origin.

## Native and remote boundaries

Launcher process creation uses a shared Windows adapter. Optional Remote Control is disabled by default, accepts predefined commands, checks a bearer token, and defaults to private/local source addresses. It shares the normal mode-command pipeline rather than changing displays directly. See [Remote Control](remote-control.md).

Builds and automated tests exercise logic and contracts. Real display-driver behaviour, DPI changes at sign-in, hotplug, game detection, and launcher focus still require Windows hardware testing.

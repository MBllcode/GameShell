# GameShell

GameShell is a local Windows app for switching the PC between gaming-focused and work-focused desktop modes.

Main runtime/storage paths:

```text
artifacts\installer\GameShell.exe
%LOCALAPPDATA%\GameShell\settings.json
%LOCALAPPDATA%\GameShell\state.json
```

## Core capabilities

- `Console Mode`: activates only the selected console display.
- `PC Gaming`: applies a saved gaming display profile (falls back to Console Mode if not configured).
- `Work Mode`: restores the desktop layout captured before the gaming session.
- Native display control through Windows DisplayConfig APIs (`QueryDisplayConfig`, `SetDisplayConfig`).
- Tray menu + global hotkeys + single-instance command forwarding.
- Per-profile launcher, mode behavior, and shortcut configuration in the UI wizard/dashboard.

## Command-line modes

```powershell
GameShell.exe --gaming
GameShell.exe --pcgaming
GameShell.exe --work
```

When GameShell is already running in tray, a second process forwards the mode command to the existing instance.

## Build

```powershell
.\build.ps1
```

What it does:

- restore .NET solution,
- build React/Vite UI,
- copy UI bundle to `src\GameShell.Desktop\wwwroot`,
- build solution.

## Package

```powershell
.\package.ps1
```

What it does:

- rebuild UI and sync desktop `wwwroot`,
- publish self-contained single-file WPF app,
- write final artifact to `artifacts\installer\GameShell.exe`.

If the destination EXE is locked (running process), script writes a timestamped fallback EXE in `artifacts\installer\`.

## UI/UX status

- Dashboard + profile flow is active after first-run wizard.
- Work Mode is moved out of quick-switch cards into the sidebar profile module.
- Step `Ekrany` validates profile name and display selection as separate local errors.
- Profile display drafts are committed deterministically before save/navigation.

## Project layout

```text
src/
  GameShell.Core/
  GameShell.Desktop/
  GameShell.Ui/
  GameShell.Windows/
tests/
  GameShell.Core.Tests/
scripts/
docs/
```

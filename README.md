# GameShell

GameShell is a local Windows app for switching between a normal desktop work setup and console-like gaming modes.

The final distributable is a single self-contained file:

```text
artifacts\installer\GameShell.exe
```

It does not require the target PC to have .NET installed. User settings are stored under:

```text
%LOCALAPPDATA%\GameShell\settings.json
```

## What It Does

- Enters `Console Mode` by activating only the selected Console display.
- Enters optional `PC Gaming` mode with a saved monitor layout.
- Enters `Work Mode` by restoring the layout captured before the gaming session.
- Uses native Windows DisplayConfig APIs for display detection and layout application.
- Hides/restores desktop icons and taskbar through the existing workflow scripts.
- Starts the configured launcher, such as Playnite, Steam Big Picture, Xbox App, a custom executable, or no launcher.
- Supports tray menu commands and global hotkeys for full GameShell modes.

## Display Modes

GameShell has one native display workflow:

- `Console Mode`: activates only the configured Console display, moves it to `0,0`, and makes it primary.
- `PC Gaming`: applies the optional saved desktop-gaming layout. If no layout is saved, it falls back to `Console Mode`.
- `Work Mode`: restores `WorkReturnLayout`, captured before entering a gaming mode.

Configuration tests use a 15-second confirmation window. Normal mode switches do not show confirmation prompts.

## Commands

The executable accepts command-line mode commands:

```powershell
GameShell.exe --gaming
GameShell.exe --pcgaming
GameShell.exe --work
```

If GameShell is already running in the tray, a second launch forwards the command to the existing instance instead of opening another tray process.

## Hotkeys

Global hotkeys are configured in `Kontroler i skróty`.

Supported format examples:

```text
Ctrl+Alt+G
Ctrl+Alt+P
Ctrl+Alt+W
```

Hotkeys trigger full GameShell workflows.

## Build

```powershell
.\build.ps1
```

The build script restores/builds the .NET solution, builds the React/Vite UI, and copies the UI output to the WPF host.

## Package

```powershell
.\package.ps1
```

Output:

```text
artifacts\installer\GameShell.exe
```

## Project Layout

```text
src/
  GameShell.Core/
  GameShell.Desktop/
  GameShell.Ui/
  GameShell.Windows/
scripts/
docs/
```

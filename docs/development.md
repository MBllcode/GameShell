# Development and verification

[← Back to README](../README.md)

## Prerequisites

- Windows x64, .NET 9 SDK, and Node.js 22.6+ with npm.
- Microsoft Edge WebView2 Runtime for the desktop UI.
- Installed launchers and real connected displays for end-to-end testing.

## Build from a clean checkout

```powershell
.\build.ps1
```

The script restores the .NET solution, installs frontend dependencies with `npm ci` when the lockfile exists, builds the React UI, copies it to the desktop host's `wwwroot`, and builds the solution. Use `-SkipNpmInstall` for subsequent builds with dependencies already installed.

```powershell
.\src\GameShell.Desktop\bin\Debug\net9.0-windows\GameShell.exe
```

`wwwroot`, `dist`, `bin`, `obj`, and TypeScript build metadata are generated and ignored. Source assets belong in `GameShell.Ui/public` or the desktop `Assets` directory. Keep `package-lock.json` committed.

## Tests

```powershell
dotnet test tests\GameShell.Core.Tests\GameShell.Core.Tests.csproj
npm --prefix src/GameShell.Ui test
```

The Windows GitHub Actions workflow runs frontend tests, a full build from source, and .NET tests. Existing focused `test:*` npm scripts are also available.

For layout work, `npm --prefix src/GameShell.Ui run dev` starts Vite. A plain browser has no native WebView bridge, so the complete app needs the desktop host or explicit test fixtures. Browser fixtures are not evidence of native display transitions.

## Package

```powershell
.\package.ps1
.\package.ps1 -SkipNpmInstall -NoLaunch
```

Packaging stops running GameShell processes, builds and stages the UI, and publishes a Release `win-x64` self-contained single-file EXE. It replaces `artifacts\installer\GameShell.exe` after publishing succeeds, then launches it unless `-NoLaunch` is set. A locked destination falls back to a timestamped EXE.

The EXE contains .NET and app assets; WebView2 and the selected game launcher are separate requirements. Keep local build outputs out of Git. Distribute binaries as release assets when preparing a release, not as source commits.

## Manual acceptance

Before sharing a runtime change, check:

1. Create/edit/save a gaming profile; reopen the app and confirm its exact behaviour flags and shortcuts.
2. Run Work → profile → Work for each intended monitor/TV setup. Confirm desktop restoration and launcher behaviour.
3. Restart Windows with the configured startup target; test tray reopening, DPI/scaling, and saved window placement.
4. Reconnect a display and confirm stable inventory, identity matching, and available refresh modes.
5. Resize the main window through intermediate widths/heights; check the footer, last controls, detected displays, and scaled canvas dragging.
6. Confirm rejected duplicate shortcuts remain local and do not change the persisted binding.

For optional LAN control, follow [Remote Control](remote-control.md) and [Home Assistant](home-assistant.md). HTTP acceptance alone does not prove a completed profile transition.

## Local data and troubleshooting

| Path | Contents |
| --- | --- |
| `%LOCALAPPDATA%\GameShell\settings.json` | Configuration, profile data, optional API token |
| `%LOCALAPPDATA%\GameShell\state.json` | Runtime state and return layouts |
| `%LOCALAPPDATA%\GameShell\Logs` | Rotated application logs |
| `%LOCALAPPDATA%\GameShell\WebView2` | WebView user data |

Do not commit local settings, tokens, logs, or user-specific diagnostics. Tests should use temporary isolated files.

The remaining `--console` / `--gaming` and `--profile` / `--pcgaming` command-line aliases support older callers. `--work` requests Work Mode; `--safe-mode` on a fresh process forces the Work startup target and opens the main window. Console Shell / Explorer replacement is no longer supported.

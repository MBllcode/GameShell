# Development and verification

[← Back to README](../README.md)

## Prerequisites

- Windows x64, .NET 10 SDK, and Node.js 22.6+ with npm.
- Microsoft Edge WebView2 Runtime for the desktop UI.
- Installed launchers and real connected displays for end-to-end testing.

## Build from a clean checkout

```powershell
.\scripts\build.ps1
```

The script restores the .NET solution, installs frontend dependencies with `npm ci` when the lockfile exists, builds the React UI, copies it to the desktop host's `wwwroot`, and builds the solution. Use `-SkipNpmInstall` for subsequent builds with dependencies already installed.

```powershell
.\src\GameShell.Desktop\bin\Debug\net10.0-windows\GameShell.exe
```

`wwwroot`, `dist`, `bin`, `obj`, and TypeScript build metadata are generated and ignored. Source assets belong in `GameShell.Ui/public` or the desktop `Assets` directory. Keep `package-lock.json` committed.

## Tests

```powershell
dotnet test tests\GameShell.Core.Tests\GameShell.Core.Tests.csproj
npm --prefix src/GameShell.Ui test
```

The Windows GitHub Actions workflow runs frontend tests, a full build from source, and .NET tests. Existing focused `test:*` npm scripts are also available. TypeScript also checks unused locals and parameters during the normal build. Test folders mirror `Profiles`, `Displays`, `Settings`, `Runtime`, `Shortcuts`, and `Host` responsibilities while retaining a single .NET test command.

For layout work, `npm --prefix src/GameShell.Ui run dev` starts Vite. A plain browser has no native WebView bridge, so the complete app needs the desktop host or explicit test fixtures. Browser fixtures are not evidence of native display transitions.

## Package

```powershell
.\scripts\package.ps1
.\scripts\package.ps1 -SkipNpmInstall -NoLaunch
```

Packaging stops running GameShell processes, builds and stages the UI, and publishes a Release `win-x64` self-contained single-file EXE. It replaces `artifacts\installer\GameShell.exe` after publishing succeeds, then launches it unless `-NoLaunch` is set. A locked destination falls back to a timestamped EXE.

The EXE contains .NET and app assets; WebView2 and the optional application are separate requirements. Keep local build outputs out of Git. Distribute binaries as release assets when preparing a release, not as source commits.

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

The remaining `--console` / `--gaming` and `--profile` / `--pcgaming` command-line aliases support older callers. `--work` requests Default setup; `--safe-mode` on a fresh process forces the Work startup target and opens the main window. Console Shell / Explorer replacement is no longer supported.


## Working on the interface

Start with [Architecture](architecture.md). Keep profile layout, application, behaviour and shortcuts in the canonical `Profile` record. Derive view maps instead of creating another synchronized state collection. Persist through the settings adapter to preserve older JSON configurations.

CSS modules are imported by `src/GameShell.Ui/src/styles.css`; the import order preserves the cascade. Check dashboard, profile displays, behaviour, application, shortcuts, default-setup settings and general settings in EN/PL at both short and narrow sizes. Include long profile names, overflowed profile lists, and no-application profiles. Read-only browser fixtures verify rendering; real monitor switching and launcher focus require Windows testing.

After profile-editing changes, verify create, edit, rename and delete. Renaming must retain the existing ID, application and shortcuts and keep the profile count unchanged. A pending display-layout commit must not overwrite changes made in later wizard steps.

For first-run changes, verify repeated Back/Next navigation, returning from Application and later profile steps to Displays, and skipping after creating a provisional profile. Returning to Displays must clear the provisional profile and its application, actions and shortcuts without changing the default setup. Back from General after skipping must return to Displays. Finishing must persist either the one final profile or no profiles, never abandoned drafts. Keep this rollback scoped to FirstRun so dashboard profiles are unaffected.

## Publishing a release

End users download the EXE or ZIP from [GitHub Releases](https://github.com/MBllcode/GameShell/releases). Build scripts stay in `scripts/` for development and CI; generated binaries stay under ignored `artifacts/`.

For a release, add notes at `docs/releases/vX.Y.Z.md`, commit the verified source and documentation, then push a version tag:

```powershell
git tag -a v0.1.0 -m "GameShell 0.1.0"
git push origin main
git push origin v0.1.0
```

Use a new version for each published release. The Windows release workflow scans Git history for secrets, audits dependencies, runs frontend/backend tests, builds the tagged source, and uploads `GameShell.exe` and `GameShell-win-x64.zip`. It publishes the release after all uploads succeed. Tags with a prerelease suffix are marked as prereleases; ordinary versions update the README's latest-download links. The workflow can also be rerun manually against an existing tag.

To reproduce assets locally:

```powershell
.\scripts\package.ps1 -SkipNpmInstall -NoLaunch -Version 0.1.0
.\scripts\create-release-assets.ps1 -Version 0.1.0
# artifacts\releases\0.1.0\
```

The packaging version override is optional; normal developer builds use the version in the desktop project. Release-asset creation checks that the EXE product version matches the requested version. ZIPs contain only the EXE, without local settings, logs, credentials or WebView cache.

## Security and license checks

CI scans full Git history with a pinned, checksum-verified Gitleaks release and
rejects high/critical npm and NuGet advisories. Dependabot checks npm, NuGet and
GitHub Actions weekly. `Directory.Build.props` enables transitive NuGet auditing;
`global.json` selects the latest installed stable .NET 10 feature band.

Before updating dependencies, refresh [license notices](licenses/README.md).
UI builds generate the offline notices page. Tests verify that every resolved
production npm package has matching notices and that the complete texts reach
the generated output. A release ZIP contains the EXE, LICENSE.txt and
THIRD_PARTY_NOTICES.txt; the EXE also contains the offline license page.

Run the history scanner on Windows with `./scripts/check-secrets.ps1`.
Do not add settings, diagnostics, private walkthroughs, or generated packages to
Git. Keep local Git author and committer identities on your GitHub noreply email.
After a history rewrite, old clones must be replaced or explicitly rebased;
merging old branches can reintroduce removed files and private metadata.

### Repository protection

Enable required CI checks for `main` and disable force pushes/deletion once the
repository is public or its plan supports private-branch protection. GitHub Free
blocks branch protection for private repositories. Native GitHub secret scanning
and private vulnerability reporting also depend on repository eligibility; the
pinned Gitleaks check in CI remains available while the repository is private.

### Dependency update review

Dependabot checks on Mondays at 09:00 Europe/Warsaw, with at most three normal npm PRs, three NuGet PRs
and one Actions PR. Minor/patch updates are grouped by React stack, frontend
libraries/tooling, .NET maintenance and Actions maintenance. SDL3-CS and its
Windows native runtime always share a group, including major updates, so they
can be tested together. Other major updates remain individual PRs.
Security updates have separate groups and are not blocked by the normal PR limit.

Review release notes, test behaviour and refresh affected license notices before
merging. Passing CI is required evidence, not permission to enable blind auto-merge.
Merged branches are deleted automatically. Close redundant PRs only after their
updates are represented in a replacement group.

For a solo-maintained protected `main`, require a pull request and passing
`windows` CI, require the branch to be up to date and conversations to be resolved,
and block force pushes/deletion. Set required approving reviews to zero: the owner
cannot approve their own PR. This retains PR/CI review without waiting for an
unavailable second maintainer. Enable these rules when the GitHub plan or public
visibility permits them.

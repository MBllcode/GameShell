# Changelog

## Unreleased

## [0.1.2] - 2026-10-27

### First-run setup

- Discard the provisional profile when Back returns to its Displays step, including its application, desktop actions and shortcuts.
- Restore **Create a profile later** and clear the draft so repeated Back/Next navigation cannot accumulate unintended profiles.
- After skipping profile creation, Back from General returns directly to the optional profile step.
- Preserve the default setup when cancelling or skipping a provisional profile.

### Interface

- Give native dropdown options opaque dark backgrounds independent of the closed control's hover state.
- Use consistent Segoe UI 9 pt text throughout the tray menu, including Open GameShell.
- Dispose rebuilt tray menu items and the owned font when they are no longer needed.

## [0.1.1] - 2026-10-27

- Bring selected applications to the foreground automatically; remove the redundant behaviour checkbox.
- Keep the profile list aligned on the left when its scrollbar appears, with consistent space around the track.
- Refresh dashboard and behaviour screenshots and their descriptions.
- Keep the personal code tour local and remove it from repository navigation.
- Simplify release downloads to the portable EXE and ZIP, without a separate checksum file.

## [0.1.0] - 2026-10-27

### Distribution and documentation

- Publish portable Windows x64 EXE and ZIP downloads through GitHub Releases, with SHA-256 checksums.
- Build and test tagged releases automatically on Windows before publishing assets.
- Refresh all six screenshots and add the optional Application page to the main README.
- Make downloads the primary entry point; keep build and packaging instructions in the developer guide.

### Code organization and maintenance

- Group backend code and namespaces by domain, and tests by responsibility.
- Co-locate React feature components and helpers; keep shared UI and WebView contracts separate.
- Store each editable profile as one record, deriving maps only for views and compatibility serialization.
- Remove unused models, bridge delegations, imports, parameters and state-free hook wrappers; enable unused-code checks in TypeScript.
- Prune unused/overridden CSS and split the remaining cascade into named modules without changing verified layouts.
- Load only EN/PL WOFF2 font subsets; classify frontend build tools as development dependencies.
- Use framework-provided Desktop DI/logging references, extract host service registration, and separate monitor catalogue policies.
- Preserve application and shortcut edits across pending layout commits; keep profile identity when renaming.
- Add updated architecture navigation and source paths.

### Transition timing and feedback

- Remove profile completion sleeps, including delays retained in older settings.
- Focus application windows as soon as they are ready, with bounded retries instead of a fixed startup sleep.
- Skip desktop-icon fallback waiting after successful restoration and verify actual native window visibility.
- Show temporary, target-specific completion confirmations without keeping controls busy; wrap sidebar status text.
- Distinguish application startup from applying a profile without an application.

### General-purpose profiles

- Use Profiles and Default setup throughout the English/Polish interface and tray.
- Make applications optional; new profiles start with no application and desktop actions disabled.
- Allow multiple profiles with identical display layouts and independent application choices.
- Configure the default setup first; allow first-run setup to finish without creating a profile.
- Keep display-only profiles active without game monitoring or launcher recovery.
- Route legacy Console activation through the profile pipeline; remove the separate Console startup target.
- Rename the session manager/models and settings resolver around profiles, share the UI application type, and remove redundant state checks.
- Preserve existing settings and command aliases; add regression coverage for migration, optional applications, and shared display layouts.


### Desktop lifecycle and data

- Remove Console Shell / Explorer replacement and its compatibility, audio-button, and shell-health services. Keep normal tray startup and legacy mode-command aliases.
- Preserve explicitly selected profile behaviour flags, including display-only profiles.
- Return independent settings snapshots and publish the settings cache only after successful saves.
- Serialize runtime-state writes, use atomic file replacement and backups, and migrate older committed startup targets.
- Restore Work Mode desktop actions even when the monitor layout already matches.

### Host and interface

- Restrict WebView commands and navigation to the local application origin.
- Declare per-monitor DPI awareness before native windows initialize.
- Add shared UI primitives, clearer profile/global settings, and updated English/Polish copy.
- Fit page content above a dedicated footer; adapt the display canvas and compensate drag coordinates for UI scaling.
- Keep layout actions beside the bounded profile-name field and align the reset button with the input.

### Repository and delivery

- Keep the README icon inline with the title and document the automatic first-launch wizard, local settings creation, and completion behaviour.

- Replace compressed desktop screenshots with 2880 × 1800 lossless captures from the running WebView renderer.
- Group build/package tooling under `scripts/` and the changelog under `docs/`, with a documentation index and updated CI commands.

- Stage frontend assets before publishing and replace the packaged EXE only after a successful publish.
- Build dependencies from the npm lockfile and support packaging without launching the app.
- Stop tracking generated frontend bundles and TypeScript cache files; retain source assets and local build outputs separately.
- Add Windows build/test CI, a single frontend test command, and a screenshot-led README with focused user/developer guides.
- Refresh Vite, nanoid, and source-map-js within the existing dependency ranges to resolve the npm audit findings.

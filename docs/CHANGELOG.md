# Changelog

## Unreleased

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

- Replace compressed desktop screenshots with 2880 × 1800 lossless captures from the running WebView renderer.
- Group build/package tooling under `scripts/` and the changelog under `docs/`, with a documentation index and updated CI commands.

- Stage frontend assets before publishing and replace the packaged EXE only after a successful publish.
- Build dependencies from the npm lockfile and support packaging without launching the app.
- Stop tracking generated frontend bundles and TypeScript cache files; retain source assets and local build outputs separately.
- Add Windows build/test CI, a single frontend test command, and a screenshot-led README with focused user/developer guides.
- Refresh Vite, nanoid, and source-map-js within the existing dependency ranges to resolve the npm audit findings.

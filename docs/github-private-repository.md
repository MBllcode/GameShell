# Private GitHub repository checklist

This project is prepared for a future private GitHub repository. This document
describes the publishing boundary; it does not create a remote, authenticate to
GitHub or push any commits.

## Before the first push

- Confirm the repository is created as **Private** in the intended GitHub
  account or organization.
- Review `git status`, `git diff --cached` and `git ls-files` for personal data,
  local paths, tokens, logs, dumps and generated binaries.
- Keep runtime data outside the repository. GameShell stores settings, state and
  logs under `%LOCALAPPDATA%\GameShell`.
- Keep generated build output ignored. The packaged executable under
  `artifacts\installer` is a local validation artifact, not source control
  input.
- Decide whether the first push should contain the existing history or a new
  clean baseline. Do not rewrite history without an explicit decision.
- Add the GitHub remote only when the destination and authentication method are
  confirmed.

## Local validation before publishing

From the repository root:

```powershell
.\build.ps1
dotnet test .\GameShell.sln --no-restore --nologo
Set-Location .\src\GameShell.Ui
npm run test:encoding
npm run build
```

For a release-like local artifact, run `.\package.ps1` and verify the
resulting executable manually. Packaging is local only and does not publish
anything.

## Repository policy

- Keep commits focused and descriptive; do not commit `%LOCALAPPDATA%\GameShell`
  contents.
- Do not commit secrets or machine-specific display identities unless they are
  anonymized test fixtures.
- Preserve the Windows DisplayConfig/CCD ABI and validate display changes on
  real hardware before changing native display code.
- Treat manual runtime validation as required for display, launcher, startup and
  desktop-shell behavior; passing tests alone is not sufficient.

## First GitHub setup (future action)

When the project is ready to publish, the intended sequence is:

1. Create an empty private repository without adding a second README or license
   that would conflict with this working tree.
2. Add the remote and verify its URL before pushing.
3. Push the reviewed branch and configure branch protection after the first
   successful push.
4. Add CI only after the local build/test commands are stable in a clean runner.

None of these steps should be performed as part of normal local development.

# Documentation

[← GameShell overview](../README.md)

| Start here | What you will find |
| --- | --- |
| [Downloads](https://github.com/MBllcode/GameShell/releases/latest) | Ready-to-run Windows EXE and ZIP |
| [User guide](user-guide.md) | Save a work layout, create profiles, configure launchers and shortcuts |
| [Architecture](architecture.md) | Project boundaries, transition pipeline, persistence and native integrations |
| [Development](development.md) | Prerequisites, build/test/package commands and manual verification |
| [Remote Control](remote-control.md) | Optional local HTTP API, authentication and endpoints |
| [Home Assistant](home-assistant.md) | REST commands, scripts and LAN verification |
| [Changelog](CHANGELOG.md) | Summary of product and engineering changes |

## Repository map

```text
GameShell.sln        Open the .NET solution
README.md           Product overview and visual walkthrough
src/                Core, Windows services, desktop host and React UI
tests/              .NET tests (frontend tests live beside the UI source)
scripts/            Build, packaging and icon generation
docs/               User/developer guides and screenshots
.github/workflows/  Windows build and test automation
```

Generated bundles, packages, dependency directories and local diagnostics stay outside version control. Screenshots in `screenshots/` are lossless PNG captures of the running app's WebView renderer; they show real configured profiles and connected displays.

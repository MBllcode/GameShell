# Optional Console Shell Mode

Console Shell Mode is an opt-in mode for dedicated Windows gaming devices. It
uses Microsoft's per-user **Custom User Interface** WinLogon policy so
GameShell, rather than `explorer.exe`, is created as the user's shell at
sign-in. The setting is disabled by default.

## Platform support and scope

GameShell configures the supported `ADMX_WinLogon/CustomShell` policy in the
current-user policy scope. Microsoft maps this policy to the `Shell` string at
`HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System`. GameShell
never changes the machine-wide Winlogon shell, and it refuses to overwrite or
remove a custom-shell policy owned by another application or administrator.
The change takes effect at the next sign-in.

Microsoft documents this user policy for Windows Pro, Enterprise, Education,
and IoT Enterprise on supported Windows 10/11 builds. Windows Home is not
supported. On an unsupported edition or OS version the UI reports that the
feature is unavailable and GameShell makes no shell change.

## Startup behavior

The Application behaviour page exposes:

- `AlwaysWorkMode`: preserve the regular desktop-oriented startup behavior.
- `RestoreLastActiveMode`: restore only the last successfully committed Work,
  Console, or named gaming profile.
- `AlwaysProfile`: activate the selected profile directly.

Successful mode commits update `state.json` immediately. Failed or transient
transitions do not overwrite the last active target. A profile startup uses the
normal mode command, session, display-stability, and launcher pipeline; it does
not introduce a separate DisplayConfig path.

Work Mode and **Open Windows Desktop** start Explorer when it is absent. When a
gaming profile is activated while Explorer is already running, GameShell uses
its existing safe taskbar/desktop hiding workflow. It does not kill arbitrary
File Explorer processes.

## Game Bar and handheld volume controls

When Console Shell Mode starts, GameShell runs a small compatibility bootstrap;
it does not start Explorer or any desktop shell surface. Xbox Game Bar is
opened through the supported `ms-gamebar:` protocol; the shell-mode hotkey
host also provides a best-effort `Win+G` registration that invokes the same
protocol without synthesizing keyboard input. In addition, GameShell
observes real HID Consumer Control input for volume buttons and first gives
native Windows handling a short opportunity. Core Audio is used only when the
physical button did not change the default render endpoint. No synthetic key
input is generated and unrelated controller input is not intercepted.

The bootstrap records a concise diagnostic snapshot of shell hosts, Game Bar
availability, running ASUS/Armoury processes, and installed ASUS services. It
does not install, start, or replace ASUS software. If a device exposes volume
buttons as multimedia hotkeys rather than raw HID reports, those specific
volume keys are also handled by the same native/fallback path.

## Recovery

If GameShell cannot complete repeated shell startups, the third consecutive
incomplete attempt enters recovery: Explorer is started and automatic profile
activation is suppressed for that run. Exiting GameShell while shell mode is
enabled also starts Explorer first.

Manual recovery is always available:

1. Press `Ctrl+Shift+Esc`.
2. In Task Manager choose **Run new task**.
3. Run `explorer.exe` for a temporary Windows desktop.

To restore the normal shell configuration, run:

```powershell
GameShell.exe --disable-console-shell
```

This removes only GameShell's current-user custom-shell policy, restores the
Explorer default, saves Console Shell Mode as disabled, and exits. For a
one-run bypass without changing the setting, run:

```powershell
GameShell.exe --safe-mode
```

Safe mode starts Explorer, suppresses automatic gaming-profile startup, and
opens the GameShell settings UI.

## Manual acceptance

Shell replacement cannot be validated by unit tests alone. Test on a supported
Windows edition using the per-user Custom User Interface policy. Verify normal
mode, AlwaysProfile boot, Work/Desktop recovery, return to a gaming profile,
repeated startup failure recovery, command-line disable, and the next sign-in
after disabling.

Microsoft references:

- <https://learn.microsoft.com/windows/client-management/mdm/policy-csp-admx-winlogon#customshell>

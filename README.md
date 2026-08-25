# GameShell

GameShell is a local Windows app for switching the PC between gaming-focused and work-focused desktop modes.

Main runtime/storage paths:

```text
artifacts\installer\GameShell.exe
%LOCALAPPDATA%\GameShell\settings.json
%LOCALAPPDATA%\GameShell\state.json
```

## Core capabilities

- `Console Mode`: compatibility path that activates only the selected display.
- `Gaming profiles`: apply user-defined display, launcher and desktop settings for any gaming setup (PC, projector, TV or another display combination).
- `Work Mode`: restores the desktop layout captured before the gaming session.
- Native display control through Windows DisplayConfig APIs (`QueryDisplayConfig`, `SetDisplayConfig`).
- Tray menu + global hotkeys + single-instance command forwarding.
- Per-profile launcher, mode behavior, and shortcut configuration in the UI wizard/dashboard.

### Display identity and capability model

Display configuration separates three concerns:

- `KnownPhysicalDisplay` is the persistent logical identity. Its canonical name
  is stable and its identity history can contain multiple Windows route aliases
  for the same physical target.
- Profile display entries store only profile-local settings such as enabled,
  primary, position, resolution, refresh policy and optional fixed refresh.
- Runtime observations are transient Windows inventory data and never create a
  second logical display merely because the friendly name, adapter LUID or
  available mode list changed.

`BestKnownCapability` belongs to the known physical display. It is historical
diagnostic information, not a minimum requirement for `AutoHighest`. After a
target is activated, GameShell enumerates the currently available active modes
and selects the highest mode exposed by Windows. It never forces a historical
mode that is not currently enumerated.

GameShell does not automatically reset monitor devices, GPUs, HDMI inputs or
TV power. If Windows exposes a reduced mode inventory, hardware/link
renegotiation may be required outside the application.

### Runtime stabilization

Display discovery separates transient Windows observations from the stable
logical display catalog. After boot, hotplug, or a topology notification,
`DisplayDiscoveryCoordinator` converges through `Loading`, `Settling`, and
`Ready` before publishing a logical display model. During settling, the UI
keeps the last stable display list and shows a lightweight synchronization
indicator; incomplete observations do not create speculative
`KnownPhysicalDisplay` records.

Profile transitions use the same stable discovery boundary before starting a
launcher. If a target topology is lost during that bounded window, GameShell
does not enter `LauncherActive`; it fails safely and uses the existing
rollback layout when available. Current Windows mode inventory remains the
authority for `AutoHighest`, so a degraded 60 Hz inventory is still valid and
is not upgraded by software.

On cold boot, when no gaming session is intentionally restored, startup
reconciliation compares the stable actual topology with configured Work Mode
and invokes the normal Work Mode path when necessary. The convergence window
is bounded and does not use a startup-specific `SetDisplayConfig` path.

The main window validates its saved rectangle against active monitor working
areas after topology stabilization. A valid custom position is preserved;
an unreachable position is moved inside the current primary monitor's work
area, including virtual-desktop layouts with negative coordinates.

### Transient UI status

The bottom operation status is derived from one backend-owned
`TransientOperationState` included in `AppStatus` and returned by `GETSTATUS`.
It carries the operation kind, phase, localized message key, start time, and
transition ID. Profile, Work Mode, startup, and display-discovery operations
map to a small set of localized phases such as `Preparing displays`,
`Synchronizing displays`, and `Restoring desktop`.

React does not maintain an independent mode-loading flag or timer for these
operations. `statusChanged` refreshes the canonical status, and a completed
or failed operation clears the transient state. Transition IDs prevent a late
completion from an older asynchronous operation from clearing a newer one.

Useful local verification commands are:

```powershell
dotnet test tests\GameShell.Core.Tests\GameShell.Core.Tests.csproj --no-restore
dotnet build GameShell.sln --no-restore
cd src\GameShell.Ui
npm.cmd run build
npm.cmd run test:runtime-status
```

Real cold-boot, Work/Profile transition, HDMI hotplug, and shutdown validation
must still be performed on the target Windows machine; build and unit tests do
not replace that runtime acceptance.

### Remote Control (Phase 1)

GameShell includes an optional, local-only Remote Control API for future LAN
integrations. It is disabled by default and exposes only predefined commands:

- `GET /api/v1/health`
- `GET /api/v1/status`
- `GET /api/v1/profiles`
- `POST /api/v1/profiles/{profileId}/activate`
- `POST /api/v1/work-mode`

Protected endpoints require `Authorization: Bearer <token>`. The token is
generated with cryptographically secure randomness, persisted in
`%LOCALAPPDATA%\GameShell\settings.json`, and is never logged. By default the
host accepts only loopback and private/local network addresses on TCP port
`47777`. GameShell does not create Windows Firewall rules; a user-managed
inbound rule may be required for another device on the same LAN.

To enable the API for development, edit the `remoteControl` section in the
local settings file and set `enabled` to `true`, preserving the generated
`authenticationToken`. This phase does not provide cloud access, Internet
exposure, OAuth, CORS, UPnP, or third-party assistant integrations.

### Home Assistant (Phase 2)

Home Assistant can use the Phase 1 API as a local LAN command source. GameShell
does not need to know which voice assistant eventually triggered the Home
Assistant script. The supported path is:

```text
Home Assistant script
  -> rest_command
  -> GameShell Remote Control API
  -> IModeCommandService
  -> normal GameShell profile transition
```

This integration does not require a custom Home Assistant component. It uses
the built-in RESTful Command integration and creates action-oriented script
entities such as `GameShell Console`, `GameShell PC`, and `GameShell Work`.
They are not switches: they represent commands and do not claim to expose a
truthful on/off state.

#### 1. Store the token in `secrets.yaml`

Create or update `secrets.yaml` in the Home Assistant configuration directory.
Store the complete authorization header value, including `Bearer`:

```yaml
gameshell_remote_authorization: "Bearer REPLACE_WITH_REAL_GAME_SHELL_TOKEN"
```

Replace the placeholder with the token from GameShell's local settings. Do
not put the token in `configuration.yaml`, a URL, a query string, a script
payload, or a voice-assistant routine. Do not commit `secrets.yaml`.

#### 2. Add the REST commands

Add this to Home Assistant's `configuration.yaml`. Replace the IP address with
the private LAN address of the GameShell PC. The profile ID is the existing
GameShell profile name/ID returned by `GET /api/v1/profiles`.

```yaml
rest_command:
  gameshell_activate_profile:
    url: "http://192.168.1.50:47777/api/v1/profiles/{{ profile_id | urlencode }}/activate"
    method: POST
    headers:
      authorization: !secret gameshell_remote_authorization
    content_type: "application/json"
    payload: "{}"

  gameshell_work_mode:
    url: "http://192.168.1.50:47777/api/v1/work-mode"
    method: POST
    headers:
      authorization: !secret gameshell_remote_authorization
    content_type: "application/json"
    payload: "{}"
```

After changing `configuration.yaml`, validate and restart Home Assistant.
`rest_command` exposes these as the actions
`rest_command.gameshell_activate_profile` and
`rest_command.gameshell_work_mode`.

#### 3. Add voice-ready scripts

Add these scripts to the script configuration loaded by your Home Assistant
instance. The `single` mode avoids stacking repeated voice/button invocations;
GameShell still applies its own transition/concurrency rules.

```yaml
script:
  gameshell_console:
    alias: GameShell Console
    mode: single
    sequence:
      - action: rest_command.gameshell_activate_profile
        data:
          profile_id: CONSOLE

  gameshell_pc:
    alias: GameShell PC
    mode: single
    sequence:
      - action: rest_command.gameshell_activate_profile
        data:
          profile_id: PC

  gameshell_work:
    alias: GameShell Work
    mode: single
    sequence:
      - action: rest_command.gameshell_work_mode
```

Change `CONSOLE` and `PC` to the actual IDs returned by GameShell. These
scripts can be called from a dashboard, automation, or later exposed through
Google Home, Alexa, or Apple Home/Siri. Example routine phrases are
`Console mode`, `PC gaming mode`, and `Work mode`; each phrase must be mapped
explicitly to the corresponding Home Assistant script by the user.

#### Optional status sensor

Status is not required to trigger a command. If high-level feedback is useful,
the following low-frequency REST sensor reads the authenticated
`GET /api/v1/status` endpoint without polling aggressively:

```yaml
sensor:
  - platform: rest
    name: GameShell Status
    resource: "http://192.168.1.50:47777/api/v1/status"
    headers:
      Authorization: !secret gameshell_remote_authorization
    value_template: "{{ value_json.mode }}"
    json_attributes:
      - sessionState
      - activeProfileId
      - activeProfileName
      - activeLauncher
    scan_interval: 30
```

#### LAN setup and troubleshooting

GameShell and Home Assistant must be mutually reachable on the same private
LAN. Remote Control must be enabled in GameShell, the configured TCP port
must be reachable, and the bearer header must match. GameShell does not create
Windows Firewall rules automatically; an inbound rule for the configured port
may be needed. Do not configure router port forwarding, UPnP, or public
Internet access.

Verify the setup in this order from the Home Assistant host:

1. Confirm the host can reach the GameShell PC on the LAN.
2. Request `GET http://<PC-IP>:47777/api/v1/health`; expect HTTP 200.
3. Request authenticated `GET /api/v1/profiles`; confirm the real profile IDs.
4. Test `rest_command.gameshell_activate_profile` from Home Assistant.
5. Run `script.gameshell_console` manually.
6. Run `script.gameshell_work` and confirm normal Work Mode restoration.
7. Repeat `Work -> Console -> Work -> PC -> Work` and confirm that GameShell
   remains the sole transition owner and does not start duplicate transitions.

The API returns `202 Accepted` when a command is accepted for asynchronous
execution, `404` for an unknown profile, `409` when another transition is
already running, and `401` for an invalid/missing bearer token. It never
provides arbitrary executable or free-form command execution.

Phase 2 does not claim direct Google Assistant, Alexa, Siri, cloud, OAuth, or
SmartThings integration. Home Assistant scripts are the provider-neutral
abstraction for a later phase.

#### Phase 2.5 manual validation

The configuration above uses the current Home Assistant syntax: `rest_command`
URLs may be templated, headers may use `!secret`, and scripts invoke actions
with `action: rest_command.*`. The optional REST sensor uses the documented
REST sensor `resource`, `headers`, `value_template`, `json_attributes`, and
`scan_interval` fields. See the official [RESTful Command](https://www.home-assistant.io/integrations/rest_command/),
[secrets](https://www.home-assistant.io/docs/configuration/secrets/),
[script](https://www.home-assistant.io/docs/scripts), and
[REST sensor](https://www.home-assistant.io/integrations/sensor.rest/)
documentation for the corresponding Home Assistant configuration rules.

This procedure is the Phase 2 exit test. It must be run against a real
Home Assistant host and a real GameShell PC on the same LAN. Build and unit
tests do not prove this integration is complete.

Set temporary shell variables on the Home Assistant host or another LAN
machine. Keep the real token outside the repository and shell history where
practical:

```sh
export GAMESHELL_PC_IP="<PC_IP>"
export GAMESHELL_REMOTE_TOKEN="<REMOTE_TOKEN>"
```

##### 1. Verify reachability and authentication

Unauthenticated health check:

```sh
curl -i --fail-with-body \
  "http://${GAMESHELL_PC_IP}:47777/api/v1/health"
```

Expected result: HTTP `200` and a response containing only `{"status":"ok"}`.

Authenticated profile check:

```sh
curl -i --fail-with-body \
  -H "Authorization: Bearer ${GAMESHELL_REMOTE_TOKEN}" \
  "http://${GAMESHELL_PC_IP}:47777/api/v1/profiles"
```

Expected result: HTTP `200` and the actual configured GameShell profile IDs.
Do not paste the token into the command output, screenshots, issue reports, or
logs.

Optional status check:

```sh
curl -i --fail-with-body \
  -H "Authorization: Bearer ${GAMESHELL_REMOTE_TOKEN}" \
  "http://${GAMESHELL_PC_IP}:47777/api/v1/status"
```

##### 2. Test the REST commands from Home Assistant

In Home Assistant, open **Developer Tools -> Actions** and invoke
`rest_command.gameshell_activate_profile` with:

```yaml
profile_id: CONSOLE
```

Use an actual ID returned by `/api/v1/profiles` if `CONSOLE` is not configured.
The expected chain is:

```text
Home Assistant
  -> HTTP POST /api/v1/profiles/CONSOLE/activate
  -> GameShell Remote Control API
  -> IModeCommandService
  -> existing CONSOLE transition
```

The HTTP response should be `202 Accepted`. Then verify the normal GameShell
runtime behavior: the configured display profile activates, current
`AutoHighest` mode selection runs, the configured launcher starts, and the
session reaches `LauncherActive`. A `202` means that GameShell accepted the
command; it is not a synchronous completion result.

Next invoke `rest_command.gameshell_work_mode` from the same Actions tool.
Expected behavior is the existing Work Mode transition and restore path, with
no remote-specific implementation.

##### 3. Test the Home Assistant scripts

Run these actions in sequence from the Home Assistant UI:

```text
script.gameshell_console
script.gameshell_work
script.gameshell_pc
script.gameshell_work
```

Verify that each command follows the same GameShell transition path as a
local UI, hotkey, or controller command. Repeat the sequence at least twice to
check that there are no duplicate transitions, stuck session state, or
authentication errors.

##### 4. Capture runtime evidence

For Console, GameShell logs should contain a concise entry equivalent to:

```text
[REMOTE COMMAND] Command=ActivateProfile ProfileId=CONSOLE Result=Accepted
```

For Work Mode, look for:

```text
[REMOTE COMMAND] Command=EnterWorkMode Result=Accepted
```

Each entry must be followed by the normal existing transition logs. Confirm
all of the following:

- the expected profile or Work Mode transition actually starts;
- there is no second transition started in parallel;
- the command is received through `IModeCommandService`, not directly by a
  display, launcher, or orchestrator service;
- no `401` authentication failure occurs;
- no bearer token or `Authorization` header appears in GameShell logs;
- the session does not remain stuck in a transitional state.

##### 5. Failure matrix

| Symptom | Checks |
| --- | --- |
| HA cannot reach `/health` | Confirm Remote Control is enabled, the PC IP and port are correct, Windows Firewall allows inbound TCP `47777`, and the two machines can route to each other on the LAN. |
| `/health` works but `/profiles` returns `401` | Check the token value, exact `Authorization: Bearer ...` format, whitespace, `!secret` name, and whether the token was regenerated in GameShell. |
| HA `rest_command` fails but `curl` works | Validate Home Assistant YAML, confirm `secrets.yaml` is in the configuration directory, restart/reload the relevant HA configuration, and inspect the rendered URL/profile ID. Do not assume GameShell is at fault until the request is observed. |
| HTTP command is accepted but GameShell does not switch | Inspect GameShell logs after `[REMOTE COMMAND]`. Confirm the existing `IModeCommandService` path received the command and then diagnose the normal profile transition. |
| HTTP `409 Conflict` | Treat it as expected when another GameShell transition is already running. Wait for it to finish and retry; do not start a parallel command. |

##### Phase 2 exit checklist

Do not mark Phase 2 complete until one real Home Assistant instance has
successfully completed all of these through the LAN API:

1. reach `/api/v1/health`;
2. authenticate against `/api/v1/profiles`;
3. activate `CONSOLE` (or another real configured profile);
4. return to Work Mode;
5. activate another configured profile;
6. return to Work Mode again.

Google Assistant, Google Home, Gemini, Alexa, Siri, Apple Home, and Assist
voice pipelines are intentionally outside this validation and remain Phase 3.

### Launchers

Profiles can launch:

- Playnite in fullscreen mode through `Playnite.FullscreenApp.exe`.
- Steam through its Big Picture URI (`steam://open/bigpicture`).
- The Xbox app through its Windows AppsFolder identity (`Microsoft.GamingApp`).

The Xbox app is currently opened in its normal windowed mode. GameShell does not pass or emulate
an Xbox-specific fullscreen switch. A future fullscreen implementation would need explicit window
management after the Xbox app starts; selecting Xbox as a launcher does not currently enable that
behavior automatically.

## Domain naming and compatibility

The product model has two user-facing areas: `Work Mode` and configurable `Gaming profiles`.
The canonical code and settings names are `GamingSettings`, `GamingProfile`, and `GamingProfiles`.
`Console Mode` is retained as a compatibility path for activating one selected display; it is not a
second gaming-profile category. Friendly display names are diagnostic only and are never used as
Samsung-specific or model-specific aliases.

Existing settings using `pcGaming*` property names are migrated when loaded. New settings are always
written with the canonical `gaming*` names. The old `--gaming` and `--pcgaming` command-line switches
remain accepted as compatibility aliases.

## Command-line modes

```powershell
GameShell.exe --console
GameShell.exe --profile
GameShell.exe --work
```

`--gaming` and `--pcgaming` remain accepted as legacy aliases. When GameShell is already running in tray, a second process forwards the mode command to the existing instance.

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
- The UI uses the GameShell Green v2 theme with dark neutral surfaces and green accents.
- Motion polish is limited to color, border, opacity, shadow, and small directional cues; it does not change layout or button geometry.
- Work Mode is moved out of quick-switch cards into the sidebar profile module.
- Step `Ekrany` validates profile name and display selection as separate local errors.
- Profile display drafts are committed deterministically before save/navigation.
- The display-profile editor now uses a fixed-height canvas for a stable visual layout, with the topology scaled to fit inside the same viewport while the right-hand properties panel aligns to the same height.
- The profile toolbar and action buttons use a more refined card-like treatment, improving visual cohesion without altering backend or display logic.
- Profile hotkeys are per-profile, normalized to canonical keys, and must remain unique across profiles.
- During hotkey capture (`NASLUCHIWANIE`), registered global hotkeys are temporarily disabled so the UI can validate duplicate combinations locally.
- A duplicate keyboard or controller shortcut remains visible in a red invalid state with its validation message for 2.8 seconds, then the field returns to its previously saved shortcut (or stays empty when there was no previous assignment). Save and dashboard navigation are blocked while the validation error is active.
- The final action in the new-profile flow is labeled `Save changes` and persists the profile before returning to the dashboard.
- `Start with Windows` uses the current user's HKCU Run entry and does not require administrator privileges; `Start minimized to tray` applies on normal app startup.
- Taskbar visibility is applied to the primary and secondary Windows taskbars, including taskbar windows nested in the shell window hierarchy.
- Sidebar navigation rows fill their available container width; their chevrons remain vertically centered and move only horizontally on hover.

## Current project status

- GameShell ships as a single bilingual build with English and Polish support, auto-detecting the Windows/system UI culture on first run and preserving the selected interface language in app settings.
- First-run mode initialization is fixed: the dashboard now hydrates the correct runtime mode instead of showing an uninitialized status after setup.
- Settings persistence is now crash-safe and atomic: writes go through `.tmp` + backup recovery and avoid in-place corruption of `settings.json`.
- The Windows session monitor now filters launcher/system processes, requires a short stabilization window before declaring a game active, and logs the real candidate/exit transitions for debug validation.
- Playnite return diagnostics remain intentionally narrow: the code logs the full recovery state and invokes the existing bounded focus restore path only after the lifecycle transition is confirmed in runtime.
- The setup flow and profile editor were tightened up across `WelcomeStep`, `DisplaysStep`, `LauncherStep`, `ProfileBehaviourStep`, `WorkBehaviourStep`, `AppBehaviourStep`, and `ControllerStep`: validation is local and deterministic, draft display topology is committed before save/navigation, and the profile wizard now follows a consistent navigation and save path.
- Windows startup behavior is now applied on normal app startup as well as fallback scenarios, and profile hotkeys are normalized/validated per profile with duplicate detection kept local to the UI while app hotkeys are temporarily disabled during capture.

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

## Repository preparation

The repository is currently prepared for a future private GitHub repository,
but no remote or publishing action is performed by the build/package scripts.
Before the first push, review the complete working tree, generated artifacts
and settings-related files. Runtime settings are stored under
`%LOCALAPPDATA%\GameShell`, not in the repository; generated files under
`artifacts\` are ignored.

# Remote Control API

[← Back to README](../README.md) · [Home Assistant setup](home-assistant.md)

The optional HTTP API lets another device on your private LAN request the same profile and Default setup commands as the UI. It is **disabled by default**.

## Enable it

Exit GameShell from the tray. In `%LOCALAPPDATA%\GameShell\settings.json`, set `remoteControl.enabled` to `true`, keeping the existing generated `authenticationToken`. The default port is `47777`; `allowPrivateNetworkOnly` defaults to `true`. Restart GameShell.

The host listens on the configured port and filters requests by their source address when private-network-only access is enabled. GameShell does not add Windows Firewall rules. For another LAN device, allow the configured TCP port on the intended private network. Keep the token private and do not publish the service through router port forwarding.

## Endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/v1/health` | Unauthenticated health check; network filter still applies |
| GET | `/api/v1/status` | Current mode, session state, profile, launcher |
| GET | `/api/v1/profiles` | Configured profile IDs and names |
| POST | `/api/v1/profiles/{profileId}/activate` | Request an existing profile |
| POST | `/api/v1/work-mode` | Request Default setup |

All endpoints except health require `Authorization: Bearer <token>`. Use the actual ID returned by `/profiles` and URL-encode it in the path.

Example from PowerShell, with a token supplied through your local environment:

```powershell
$gameShellBase = 'http://192.168.1.50:47777'
$gameShellHeaders = @{ Authorization = "Bearer $env:GAMESHELL_REMOTE_TOKEN" }
Invoke-RestMethod "$gameShellBase/api/v1/health"
Invoke-RestMethod "$gameShellBase/api/v1/profiles" -Headers $gameShellHeaders
Invoke-RestMethod "$gameShellBase/api/v1/work-mode" -Method Post -Headers $gameShellHeaders
```

Replace the example address with your PC's private LAN address. Do not paste the real token into screenshots, issue reports, or repository files.

## Results and limitations

| Status | Meaning |
| --- | --- |
| `200` | Read request succeeded |
| `202` | Command accepted for asynchronous execution |
| `401` | Missing or incorrect bearer token |
| `403` | Source rejected by the private/local network filter |
| `404` | Unknown route or profile |
| `409` | Another transition is already running |

`202` confirms dispatch, not completion. Check `/status`, the UI, and local logs for the resulting transition. Commands use `IModeCommandService`; there is no arbitrary executable or free-form command endpoint.

This is a local HTTP integration, not a cloud service. It does not provide TLS, OAuth, or direct Alexa/Google/Siri account linking. Home Assistant can call it through REST commands. Real LAN and hardware validation is required independently of unit tests.

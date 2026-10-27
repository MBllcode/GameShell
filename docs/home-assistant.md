# Home Assistant

[← Back to README](../README.md) · [API reference](remote-control.md)

Use Home Assistant's REST commands to activate a gaming profile or return to Default setup. First enable [Remote Control](remote-control.md), check LAN connectivity, and read the real profile IDs from `/api/v1/profiles`.

## 1. Keep the token in secrets.yaml

In your Home Assistant configuration, store the complete authorization value:

```yaml
gameshell_remote_authorization: "Bearer REPLACE_WITH_YOUR_GAME_SHELL_TOKEN"
```

Keep `secrets.yaml` out of source control.

## 2. Define REST commands

Add to `configuration.yaml`, replacing the example IP with your GameShell PC's private address:

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

Validate your configuration and restart Home Assistant.

## 3. Add scripts

If your configuration already has `script: !include scripts.yaml`, place the entries below in `scripts.yaml` **without** the outer `script:` key. Otherwise add the complete block under `script:` in `configuration.yaml`.

```yaml
script:
  gameshell_gaming:
    alias: GameShell Gaming
    mode: single
    sequence:
      - action: rest_command.gameshell_activate_profile
        data:
          profile_id: REPLACE_WITH_PROFILE_ID

  gameshell_work:
    alias: GameShell Work
    mode: single
    sequence:
      - action: rest_command.gameshell_work_mode
```

Use an actual ID returned by GameShell rather than assuming the profile name is the ID. These are command scripts, not switches with persistent on/off state. Exposing them to a voice assistant is a separate Home Assistant configuration step.

## Verify the full path

1. From the Home Assistant host, request `/api/v1/health`; expect `200`.
2. Request `/api/v1/profiles` with the bearer header; expect the configured profiles.
3. In **Developer Tools → Actions**, run `rest_command.gameshell_activate_profile` with `profile_id` set to a real ID.
4. Confirm the profile actually changes displays and starts its launcher when enabled.
5. Run `script.gameshell_work`; confirm the normal Default setup restoration.
6. Repeat with another profile and check that no duplicate transition starts.

An accepted command returns `202`; verify the finished result in GameShell. `401` points to token/header issues, `409` means a transition is busy, and no HTTP response usually points to routing/firewall/host configuration. GameShell logs accepted requests as `[REMOTE COMMAND]` followed by normal transition logs; never include the token in shared evidence.

Automated tests do not prove a live Home Assistant integration. Test the sequence above on your own LAN before relying on it.

Reference: [RESTful Command](https://www.home-assistant.io/integrations/rest_command/), [secrets](https://www.home-assistant.io/docs/configuration/secrets/), [scripts](https://www.home-assistant.io/docs/scripts/).

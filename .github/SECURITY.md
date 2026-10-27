# Security

## Supported versions

Security fixes are made on `main` and shipped in the latest stable release.
Update to the latest release before reporting a problem.

## Reporting a vulnerability

Use **Security → Report a vulnerability** on GitHub when private reporting is
available. Please do not post tokens, settings files, logs containing personal
paths, or exploitation details in a public issue.

If private reporting is unavailable, open an issue containing only a request
for a private security contact. Wait for a private channel before sharing details.

Include the affected version, Windows version, reproduction steps, and expected
impact. A response time is not guaranteed for this personal project.

## Security boundaries

- Remote Control is disabled by default. When enabled, its HTTP bearer token
  must stay private. Use it only on a trusted private network; do not expose the
  port to the internet. The private-address filter does not encrypt traffic.
- WebView commands are accepted only from the bundled application's trusted
  origin. External pages must not be given bridge access.
- Inspect code and downloaded releases before running them. Release binaries
  are currently unsigned.

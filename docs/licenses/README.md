# Licenses and third-party notices

GameShell's own code is licensed under [MIT](../../LICENSE). Third-party
components keep their original licenses; the project license does not replace them.

[`components.json`](components.json) records the component versions reviewed for
this release and the original license/notice files copied from their npm or NuGet
packages. Native SDL's notice comes from its matching upstream release tag.
The inventory includes resolved production npm packages; some optional tooling
may not be included in the final JavaScript bundle.

The .NET entries record the license snapshot reviewed for the self-contained
Windows runtime. When updating the SDK/runtime or a dependency, review the
upstream terms, refresh the relevant texts and version, and run `npm test`.
Do not edit the original license texts to substitute GameShell's copyright.

`scripts/generate-notices.mjs` combines these texts with GameShell's license.
It runs before UI builds, development startup and tests. Generated outputs are
ignored by Git:

- An offline license page bundled inside the EXE, accessible from **General →
  Licenses and third-party notices**.
- `artifacts/licenses/THIRD_PARTY_NOTICES.txt`, included in release ZIPs together
  with `LICENSE.txt` and the executable.

Playnite, Steam and Xbox names and logos identify optional third-party products.
They belong to their respective owners, are excluded from the GameShell MIT grant,
and do not imply affiliation or endorsement.

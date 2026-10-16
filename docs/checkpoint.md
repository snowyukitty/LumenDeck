# Current checkpoint

Updated 2026-10-16. **LumenDeck 1.4.2** makes theme selection visible and explains
material states. The desktop application was rebuilt, installed and started from
its desktop shortcut. No release or release tag has been published.

## Delivered behavior

- A quiet **Theme · <finish>** button sits immediately left of Settings. One click
  opens Smoke, Frost and Ceramic, followed by Mica, Glass and Solid material.
- The menu explains pending saves, protected settings, Windows high contrast,
  Ceramic's solid surface and system transparency fallback. Ceramic leaves
  unavailable material choices unchecked; its previous preference is retained.
- Theme and Settings > Appearance share the same builder and confirmed-save path.
  The visible label and accessibility name follow the active finish.
- The minimum-size header fits all three finish names and both Night saver
  captions without overlap or clipped button text. Night saver remains rightmost.
- All existing display controls, presets, scheduling, recovery and classic
  advanced controls use the same controller. This update changes presentation
  and shared version metadata, not the monitor-control implementation.

See [material interface](material-interface.md), [appearance](themes.md),
[UI design](ui-design.md) and [Night saver](night-saver.md).

## Verification actually completed

| Check | Result |
| --- | --- |
| Release solution build, warnings as errors | Passed; zero warnings/errors |
| Full AppTests | 2,722 passed |
| Committed-source material checks and capture | 135 passed |
| Release assembly path checks and privacy canaries | Passed; all four product assemblies |
| Extracted portable package | Allowlist, hashes, versions, bundled paths, standalone CLI and WPF/WinForms startup passed |
| Installer regressions | 24 passed |
| Bundle regressions | 22 passed |
| ControlTests | Not rerun locally for this presentation-only update; unchanged suite remains in hosted CI |

The actual Theme menu selections cover every finish and material, persistence,
no device writes/rescans, draft preservation, active Night saver preservation and
pending/protected/failed-save feedback. Header measurements cover the minimum
660 x 610 DIP window. They do not establish every Windows text size or DPI.
The earlier 1.4.1 preference-test timing repair remains in place.

Six opaque window references and a theme-menu render were generated from
`cdf6fb2e187fdea6f2ed7d6b1888cbb939e42fb3`; the [receipt](assets/material-reference.json)
records their hashes. Representative regular/compact renders and menu states were
visually inspected. The package source below adds only these reference assets.

## Candidate and installed desktop app

Package source: `64a6a8a6c93943952b399ad684c10f5c94fedbf8`.
Archive: `dist/build-ef1719c4dd2c4dc0ba12ba411a09de26/verified/LumenDeck-1.4.2-win-x64.zip`.
SHA-256: `c3aa3d9b122f43551d5aa47681fe2ca4dcc6a512845be7d2846386fbe4f3c1d9`.
Both executables report `1.4.2+64a6a8a6c93943952b399ad684c10f5c94fedbf8`.
The [release record](release-readiness.md) is checked against the actual archive.

The installed GUI/CLI hashes match that payload. Desktop/Start Menu shortcuts
point to the installed app; all three top-level settings files were unchanged
before startup. Exactly one running process reported the expected 1.4.2 build,
a responsive visible main window and no startup error-log growth. Prior installed
binaries, shortcuts and settings backups remain private. Normal close was
attempted; the old keep-running instance was then stopped for replacement.
Installation/startup verification does not establish physical monitor acceptance.

## GitHub and remaining acceptance

The finished checkpoint is merged locally and pushed to main after local checks.
The workflow runs on every branch, so intermediate branches are not pushed.
The final handoff and ignored installation record report the actual result from
[GitHub Actions](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml).
The push creates no release, tag or GitHub Release asset.

The design analysis was based on source and reference-layout descriptions; it did
not inspect desktop blur. The previously unavailable native capture helper was
not used. Reference images force opaque rendering. Foreground keyboard/pointer
interaction, Narrator, actual Windows contrast themes, mixed-DPI movement,
physical brightness/gamma/restoration and clean-profile installation remain
manual acceptance. Fixture success does not establish these outcomes.

Scope: material header/menu feedback, tests, shared version, documentation,
synthetic references and local package/update. No sibling project files were
modified. The project lease is released at handoff. Resume from Git state,
this record and ignored `artifacts/themes-142-install-path.txt` and
`artifacts/material-handoff.json`.

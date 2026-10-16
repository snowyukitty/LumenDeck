# Current checkpoint

## Material design follow-up

The owner requested a material and hierarchy redesign after reviewing the palette
themes. An isolated [native material study](../experiments/MaterialStudy/README.md)
now compares Smoke, Frost and Ceramic with Mica/Acrylic/opaque backdrops. It has
synthetic displays and no hardware or product-settings dependency. Its Release
build and 32 preview checks passed; six reference renders cover regular and
compact layouts. DWM accepted Mica/Acrylic requests on the local host, but actual
compositor visual acceptance remains unverified because the native capture helper
was unavailable. The production client and package below are unchanged.

The pending palette-only desktop replacement is **on hold for design review**;
do not automatically install it after an Exit reply. A preview is not a production
upgrade. Framework selection and integration remain open decisions.

## Verified production baseline

Updated 2026-10-16. **LumenDeck 1.4.1** adds persistent live themes while keeping
the accepted compact controls and the Night saver repair. Work started from
`25cb8e0` on `feat/multiple-themes`.

## Behavior

- Settings > Theme offers Amber, Paper, Ocean and Violet. Confirmed saves update
  existing windows, editors, menus, native frame intent and cached card surfaces.
- Theme switching keeps display selection, geometry, scroll position and input
  drafts/selection. It performs no display scan, gamma, brightness or startup write.
- Windows high contrast takes priority, including controls first created in that
  mode. Returning to ordinary colours restores the saved semantic palette.
- Failed saves keep the previous live palette/check; pending or protected storage
  blocks competing requests. Unknown IDs fall back without being rewritten until
  an explicit selection. The account preference is stored as `ThemeId`.
- Muted chip backgrounds improve text contrast. Fonts and sizes stay shared;
  subscriptions and cached bitmaps are released with their owning controls.
- Night saver remains first in the toolbar, requests at most 5% hardware brightness
  on the kept display and retains recovery snapshots when the theme changes.

Immutable product/package/capture source:
`cc08d8aac9e751205c2e076fd721c789dc6df220`.
The GUI, Core and CLI remain version 1.4.1. See [themes](themes.md),
[Night saver design](night-saver.md), [UI design](ui-design.md) and the
[source-bound preview receipt](assets/screenshot.json).

## Verification

| Check actually run | Result |
| --- | --- |
| Release solution build, warnings as errors | Passed, zero warnings/errors |
| Full ControlTests | 4,610 passed |
| Full AppTests | 2,612 passed |
| Final focused live theme checks | 228 passed |
| Final command appearance/activation checks | 97 passed |
| Committed-source native showcase | 40 passed |
| Capture exporter regressions | 6 tests passed |
| Native DPI round trips in full AppTests | 125%/150% passed existing layout-work and no-drift limits |
| Release source-path/privacy canaries | Passed |
| Extracted package validation | Allowlist, hashes, versions, bundled paths, standalone CLI and synthetic GUI startup passed |

The suites ran sequentially. Theme checks cover text/primary/focus/chip contrast,
open editor drafts and selection, storage refusal/pending/conflict states, native
frame intent, system-colour messages, subscription disposal, startup/fallback and
active Night saver preservation. The final committed-source run includes explicit fallback selection. Package
regressions passed, including 24 installer and 20 bundle checks.

All ten public previews use fictitious displays and committed source. Native
Paper/Ocean/Violet menus and windows, compact Amber and portfolio derivatives were
visually reviewed. Real settings, display identities, backups and installation
receipts remain local and ignored. Logs use ignored `artifacts/themes-*.log`.

## Package and desktop update

Verified archive:
`dist/build-0bef1bd6963945098e6228399594205a/verified/LumenDeck-1.4.1-win-x64.zip`.
SHA-256: `acf611ffed5ae6b61f2aa75c69d170643e44f0da0c88077b74cc2aec98098bf5`.
Both executables report `1.4.1+cc08d8aac9e751205c2e076fd721c789dc6df220`.
[Release readiness](release-readiness.md) is the machine-checked identity record.

The previous installed 1.4.1 build (`31af655`) and shortcuts are backed up, and
this replacement payload is verified. Normal tray Exit is required so restoration
and pending saves finish; the installer refuses a running app. Installation checks
compare payload hashes, shortcut targets and saved-state hashes before startup,
then verify the running path/version, responsive window and error-log growth.
The final handoff and ignored installation receipt record the actual outcome.

## GitHub and remaining acceptance

The finished checkpoint is fast-forwarded to main and pushed after local verification.
The unchanged workflow runs on every branch, so only the completed main checkpoint
is pushed. The final handoff reports the actual hosted result from
[GitHub Actions](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml).
No release, release tag or downloadable release asset is published by this push.

Real Windows contrast-theme switching, foreground interaction, DWM material,
Narrator speech, physical darkness/restoration, hot-plug/interrupted recovery and
clean-profile install/uninstall remain separate acceptance. Fixture checks do not
establish those outcomes.

Scope: this repository's multi-theme presentation, regression checks, documentation,
synthetic previews and local package/update. No sibling files or artwork were copied.
The project lease is released at handoff; resume by checking Git state, this checkpoint
and the ignored installation receipt.

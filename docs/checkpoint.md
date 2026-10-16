# Current checkpoint

Updated 2026-10-16. **LumenDeck 1.4.3** refines material layering and contrast.
The fresh portable package is installed, and the desktop shortcut starts one
responsive 1.4.3 process. Existing appearance preferences are preserved.
No release or release tag is published.

## Delivered behavior

- Glass and Mica use distinct authored surface recipes. Glass retains more native
  contribution through the card while placing tint behind outer text.
- A directional content rim and short contact edge establish depth. Secondary
  text, accent and slider-track colors are stronger. Solid fallback and reference
  renders are opaque; high contrast overrides authored decoration.
- Theme remains visible beside Settings. Its menu explains wallpaper-informed
  Mica and Windows-controlled Glass activation/fallback. Saved choices are retained.
- Display geometry, compact values, Night saver, presets, exact entry and advanced
  controls continue through the same controller and monitor implementation.

See [material interface](material-interface.md#material-layering-in-143),
[appearance](themes.md) and the [controlled bench](../experiments/MaterialBench/README.md).

## Verification actually completed

| Check | Result |
| --- | --- |
| Release solution and bench, warnings as errors | Zero warnings/errors |
| Full AppTests | 2,846 passed |
| Focused material integration | 232 passed; the earlier committed-source reference export passed 253 checks |
| Release source-path checks and privacy canaries | Passed; all four product assemblies |
| Extracted portable package | Allowlist, hashes, versions, bundled paths, standalone CLI and synthetic WPF startup passed |
| Installer state regressions | 24 passed |
| Bundle regressions | 22 passed |
| Installed payload | GUI/CLI hashes, shortcuts, three preserved state files and responsive startup verified |
| ControlTests | Unchanged; not rerun locally, retained in hosted CI |

Contrast preflight tests extreme compositor colors for selected Glass text and
slider-track tokens using encoded-alpha composition. They are not rendered-pixel
accessibility certification. Existing material tests retain exact-entry drafts,
confirmed saves, no device writes on appearance changes and active Night saver.

The first [hosted run](https://github.com/snowyukitty/LumenDeck/actions/runs/38851921926)
failed the material-choice assertion after passing ControlTests. A held ordinary
checkpoint reproduced the failure: synthetic Click was raised on an unavailable
material item while saving, and the controller correctly retained Mica. The test
now drains the queued worker/debounced checkpoint before observing appearance and
asserts each menu item's admission. Device-write, draft and persisted-choice checks
remain; no product behavior or workflow assertion was relaxed. Failure/reproduction
logs are retained privately. The final hosted result is reported at handoff.

Six opaque window references and the Theme menu were regenerated from
`5ab486820713fe4cf9e3f34462bda4bd45216b7d`; their [receipt](assets/material-reference.json)
records source and hashes. Regular/compact references were visually inspected.
The package source adds only those synthetic reference assets.

## Installed desktop identity

Package source: `4c6efe1ba0b6a1e5799ac5621eca4d0f99ac22ef`.
Archive: `dist/build-63f795a90f5b49adbc4695d7fa25193a/verified/LumenDeck-1.4.3-win-x64.zip`.
SHA-256: `2d0eb2977dd2b155d807641d04ce04fda0effd0824d4b2eef531799ced4b4bc6`.
Both executables report `1.4.3+4c6efe1ba0b6a1e5799ac5621eca4d0f99ac22ef`.
The [release record](release-readiness.md) is checked against the archive payload.

Desktop/Start Menu shortcuts target the installed app. All three top-level state
files were unchanged before startup; appearance preferences and zero active
recovery records were confirmed after startup. Exactly one process reported the
expected build, a visible responsive main window and zero startup error-log growth.
Normal close was attempted; the keep-running instance was then force-terminated
under the owner's standing authorization. Prior binaries, state and installation
receipts remain in private backups. Installation is not physical monitor acceptance.

## Native material evidence and remaining acceptance

The baseline active-only Smoke/Glass pair showed background response much stronger
in the foundation than in the card; focus loss prevented its Solid null control.
Complete baseline/candidate visible-only runs produced zero A/B ROI differences
with native foreground unverified. The candidate active-only attempt timed out.
Those runs are retained as partial/blocked evidence, not rewritten as passes.
Native active Glass quality remains unverified. Windows controls inactive Acrylic
appearance; DWM readback does not establish compositor output.

Foreground input, actual contrast themes/Narrator, mixed-DPI motion, hardware
brightness/gamma/restoration and clean-profile installation remain acceptance work.
No physical monitor operations were performed by the bench or synthetic tests.

The finished checkpoint is merged locally and pushed once to main after local
checks. The workflow runs on every branch, so intermediate branches are not pushed.
The final handoff reports the actual [hosted result](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml);
no hosted success is inferred from local tests. No release/tag is created.
Scope: material presentation/bench, tests, version, docs, reference assets and
verified local package/update. The project lease is released at handoff. Resume
from Git state and ignored `artifacts/material143-install-path.txt`.

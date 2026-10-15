# Current checkpoint

Updated 2026-10-15. This milestone completes the **1.4.0 display-layout and
adjustment-reliability repair candidate**, following review of a real staggered
desk screenshot. Work started from clean source `1c532f5` on
`fix/project-review-20261015`; unrelated files were preserved.

## Delivered behavior

- Scaled brightness captions stay on one line and principal percentages retain
  their full height, including cards built after asynchronous display discovery.
- Staggered schematic displays retain readable identifiers. Header/map sections
  settle without overlap after compact-window and DPI transitions.
- Nested Night saver controls wrap inside their command row. Native fractional-DPI
  transitions settle parent widths before measuring children and avoid an extra
  command-height rounding correction.
- Refused GUI gamma requests restore the prior model/readout and preserve saved
  warmth and Custom. Successful retry still persists normally.
- Explicit CLI requests report unsupported/refused brightness or contrast without
  replacing Custom. Signed percentage steps handle surrounding whitespace consistently.
- Public previews now use actual native DPI, including minimum and stacked windows.
  Their original-pixel master, uniform derivatives and capture/build hashes bind the
  evidence to committed source and reject stale or altered inputs.

The implementation and capture tools are committed at
`6a0bd4ed17cb8c908f119c555573ae8e99a00503`; documentation and generated images follow
that immutable source. The product repairs themselves were introduced in `114eed4`.
See [repair details](repair-review.md), [capture method](showcase.md) and
[image receipt](assets/screenshot.json).

## Verification

| Check actually run | Result |
| --- | --- |
| Release solution and both test-project builds, warnings as errors | Passed, no warnings/errors |
| Full ControlTests on the repaired product | 4,586 passed; product source unchanged by subsequent capture tooling |
| Full AppTests including the new showcase layouts | 2,224 passed |
| Focused native showcase generation from committed source | 30 passed |
| Capture exporter regressions | 6 tests passed, including stale source/build, changed pixels/dimensions and invalid portfolio masters |
| Screenshot-like staggered displays after native DPI rebuild | Passed at 125% and 150% |
| Native cross-display DPI round trips | 64 passed with existing layout-work limits |
| Release source-path/privacy canaries | Passed |
| Extracted portable package and package regressions | Passed; allowlist, hashes, versions, startup probes and refusal/state-preservation cases |
| Candidate identity record against archive and both executables | Passed |

The complete native documentation, minimum and stacked images, portfolio master and
1200x750/480x300/320x200 derivatives were visually inspected. Captures use fictitious
displays on a private desktop. The owner's screenshot and monitor identities are not
published. Full AppTests includes synthetic 100/150/200% layout coverage; native
125/150% transitions are separate evidence. Neither establishes foreground dragging
or physical driver acceptance. Detailed local logs remain under the ignored
`artifacts/review-20261015` directory.

## Portable candidate and GitHub handoff

The verified local package is
`dist/build-0128e70c80d94f90a86044843e65337a/verified/LumenDeck-1.4.0-win-x64.zip`.
SHA-256: `4076253b458fcea46aa7a93438a029b6973863b8191cab1175ab6fdee57d4487`.
Both executables report `1.4.0+6a0bd4ed17cb8c908f119c555573ae8e99a00503`, file version
`1.4.0.0`. [Release readiness](release-readiness.md) is the single current identity
record consumed by `Check-ReleaseRecord.ps1`. Previous candidates are retained.

The owner requested a GitHub checkpoint. The completed unit is fast-forwarded to
`main` and pushed once after local verification. The existing workflow runs on all
branches, builds and tests on Windows, and uploads a verified CI package; it does
not publish releases or deploy the app. No workflow was weakened or disabled.
Check the exact pushed revision in [GitHub Actions](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml);
local results do not imply hosted success. The session handoff reports the actual
remote outcome without another documentation-only CI run.

No release/tag or installed-app replacement is part of this checkpoint. The running
installation remains separate from this new portable candidate.

## Resume and remaining acceptance

Physical DDC/WMI/gamma, Night saver/blackout restoration, hot-plug/interrupted
recovery and emitted-light behavior were not exercised. A hardware acceptance pass
should preserve initial settings and verify restoration. Foreground mixed-DPI
pointer dragging, compositor appearance, Narrator speech, real contrast-theme
switching and clean-machine install/update/uninstall remain manual acceptance.

Scope was this repository: product UI/CLI repairs, regression/capture tools,
generated synthetic previews, changelog and developer documentation. The project
lease is released at handoff. Resume by checking Git status and this checkpoint;
keep source, portable candidate, installed application and published release distinct.

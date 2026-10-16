# LumenDeck presentation

The presentation uses flat charcoal, warm paper, amber type and an unmasked
interface plate. The README introduces the desk workflow before the protocol
details. Large serif type supplies identity at small sizes; a separate portfolio
composition reduces the English copy. The light triptych belongs in the appearance
guide, where comparison matters more than small-card recognition.

## Deliverables

| File under `assets/promotion/` | Use |
| --- | --- |
| [readme-1600x1000.png](assets/promotion/readme-1600x1000.png) | README opening |
| [social-1280x640.png](assets/promotion/social-1280x640.png) | Prepared GitHub social preview |
| [portfolio-1200x750.webp](assets/promotion/portfolio-1200x750.webp) | Personal-site card |
| [portfolio-480x300.png](assets/promotion/portfolio-480x300.png) | Delivered-size review |
| [portfolio-320x200.png](assets/promotion/portfolio-320x200.png) | Compact delivered-size review |
| [finishes-1600x920.png](assets/promotion/finishes-1600x920.png) | Appearance comparison |
| [receipt.json](assets/promotion/receipt.json) | Reference identity and output hashes |

Each master also has an editable SVG alongside it, with embedded reference pixels
and no remote fonts or images. The shipped app icon is preserved. These posters
are compositions, not desktop screenshots. Complete opaque WPF client references
are uniformly reduced without cropping, perspective, invented controls or blur.
The sample displays establish layout, not hardware acceptance or measured light.

The source checkpoint is 1.5.0, not a published release. The existing v1.0.0
download remains separately identified in the README. No release or tag is created
by this work. The social file is prepared; repository Settings has not been changed.

## Repeat

On Windows with the .NET 10 SDK, Pillow, Playwright and its Chromium installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/capture-material.ps1
python scripts/build-promotion.py
```

The capture harness uses a private desktop and synthetic transports. The artwork
builder verifies every input SHA-256 against `material-reference.json`, embeds the
references, renders locally without network access, checks actual font bounds for
clipping and image overlap, then compares each interface plate against an independent
render of its source. The maximum permitted encoded-channel delta is one for browser
rounding, recorded per plate. It exports PNG and lossless WebP derivatives.
Local Georgia and Segoe UI fonts must be present; rendered typography can differ
on another operating system. Tool versions, font hashes and asset hashes record the
output actually generated.

Inspect the PNGs at native size, especially 480x300 and 320x200. Fine interface
text is not expected to be readable in a small card; every README claim and the
development disclosure is also present as selectable text, with a full-size link.

## Collaboration and decisions

ATD advisory synthesis used `claude-code:gld`, explicitly selected and provider-
reported `claude-opus-5-5`. Adopted its specimen-plate direction, registration ticks,
large serif identity, separate small-card composition, light comparison plate and
visible release distinction. Used installed system fonts rather than adding font
downloads. Omitted the optional Night saver diagram to keep the real UI dominant.
The normalized consultation stays in ignored `artifacts/promotion/`.

The source-based consultation did not inspect rendered images. Local rendered
review remains separate from Claude's advice and from owner aesthetic acceptance.
Its scheduling contradiction was checked against `PresetScheduleView` and fixed.
New warmth copy names a software gamma tint rather than a medical benefit.

The final source critique used the same exact route and model; it was collaborative,
not independent review. Addressed development disclosure on the website, tick/rule
alignment, header baseline, explicit rendering viewports, tool/font identity and
double lossy compression. The website importer also requires the reviewed reference
commit as an explicit argument. Hash checks establish integrity, not a signature or
owner approval.

## Verification

- Fresh references: 262 material integration and 267 preset checks passed, using
  synthetic displays and isolated settings on a private desktop.
- Existing showcase exporter: all six regression tests passed.
- All four artwork masters pass measured text bounds and source-plate comparisons.
- PNG review covers the README, social preview, three-finish comparison and both
  delivered card sizes. Native composited desktop appearance remains untested.
- Website verification is recorded in its scoped design handoff. Publication and
  deployment state must be reported separately from these local checks.

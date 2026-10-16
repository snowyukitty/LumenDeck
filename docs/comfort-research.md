# Comfort presets: evidence and design decisions

Reviewed 2026-10-16. This is the rationale for the desktop presets and offline
Comfort guide, not a clinical validation of LumenDeck.

## What the evidence supports

- [OSHA workstation environment](https://www.osha.gov/etools/computer-workstations/workstation-environment)
  recommends controlling reflections and avoiding large differences between a
  display and its surroundings. This supports matching screen brightness to the
  room rather than always making it dimmer.
- [OSHA monitors](https://www.osha.gov/etools/computer-workstations/components/monitors/)
  describes viewing distance, screen position and legibility. The guide prioritizes
  readable text, reducing glare and adjusting the desk as well as software.
- [AOA computer vision syndrome](https://www.aoa.org/healthy-eyes/eye-and-vision-conditions/computer-vision-syndrome)
  recommends breaks, blinking and an appropriate workstation. Its
  [20-20-20 reference](https://www.aoa.org/AOA/Images/Patients/Eye%20Conditions/20-20-20-rule.pdf)
  is a practical habit, not evidence that a precise timer interval treats all symptoms.
- [Brown et al., PLOS Biology, 2022](https://doi.org/10.1371/journal.pbio.3001571)
  recommends lower evening light exposure beginning at least three hours before
  habitual bedtime, for healthy adults with regular daytime routines. Its units
  are melanopic equivalent daylight illuminance **at the eye**, including room
  light. Neither estimated screen nits nor a gamma-tint kelvin value can establish
  compliance. The advice does not override visual needs or apply identically to
  shift workers and every other population.
- [Duraccio et al., Sleep Health, 2021](https://pubmed.ncbi.nlm.nih.gov/33867308/)
  randomized 167 young adults to iPhone use with or without Night Shift, or no
  phone use, before bed for seven nights. There was no overall difference in
  sleep outcomes across groups. This is evidence against promising sleep benefits
  from a warmer screen alone, not a test of this desktop application.
- [Cochrane, Singh et al., 2023](https://doi.org/10.1002/14651858.CD013244.pub2)
  found little or no short-term eye-strain benefit from blue-light-filtering
  **spectacle lenses**; sleep evidence was uncertain. Lenses are not software
  filters. We do not extrapolate a clinical effect to LumenDeck in either direction.

## Product choices, not research-prescribed values

No source above establishes a universally comfortable monitor setting. These
defaults are deliberately editable engineering starting points:

| Mode | Estimated target | Requested tint | Intended starting context |
| --- | ---: | ---: | --- |
| Day | 160 nits | 6500 K | Lit room; original colour ramp |
| Evening | 100 nits | 5000 K | Softer room light; mild warmth |
| Night | 60 nits | 4000 K | Dim room with background light |
| Custom | Per-display manual levels | Per-display manual tint | Individual preference |
| Night saver | At most 5% hardware range | Unchanged | Temporary dim desk; unused screens covered |

Day's former 200-nit target could be unnecessarily bright for indoor reading.
Evening and Night now offer more separated, lower starting points than 150 and
110 nits. The warmer tints are optional preferences, not eye-protection or sleep
claims. Raise brightness when reading becomes difficult; use neutral warmth for
colour-sensitive work and verify calibration. No automatic contrast change is
introduced: the hardware contrast control is not a universal readability scale.

Night saver retains its existing reversible behavior and recovery records. Its
5% setting is not promoted as a reading recommendation. The UI now explicitly
offers raising brightness or ending the session when it is too dim.

## Limits and persistence

Panel estimates are linear approximations. A generic profile cannot establish
physical luminance. Targets below the profile floor or above its peak are clamped;
the editor now states the limiting estimate and that the target cannot be reached.
Warmth can also change emitted light; the nits model does not compensate for it.
HDR, other colour software, the display and driver can affect the result.

Saved preset overrides and Custom values remain intact. Only modes with no saved
override adopt the new defaults. Saving targets does not apply them; a subsequent
preset request does. An already enabled schedule can request the new default at
startup or the next period, following existing schedule behavior.

The offline guide is reachable from both interfaces and the preset editor.
Material mode buttons expose the **active saved targets**, rather than fixed
default tooltips. Guidance is available without a network request or hardware write.

## Verification boundary

Automated checks cover target persistence, preset/CLI/schedule routing, preview
limits, guide reachability and layout with synthetic displays. They do not measure
light, diagnose symptoms or establish that these defaults feel comfortable to a
particular person. User feedback and instrument measurements remain distinct.

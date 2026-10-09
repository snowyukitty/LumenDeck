# Support report contract

Use Connection help > View support report, or `lumendeck-cli --support-report`.
Review the report before choosing Copy or sharing its console output. Nothing
is uploaded automatically. The report minimizes data; display counts, native
control ranges, backends and OS versions can still distinguish a setup.

The GUI captures the latest app scan into a separate report object. Opening the
connection guide from the reading/empty/failure viewport uses that same observation,
without starting a new scan. An already open guide updates when the current scan
settles. Reading and failure omit previous display details; a completed empty scan
has a completed state and zero displays. A report preview already open remains
a snapshot, as described below.

Opening the preview does not scan hardware or access the clipboard. Copy sends the exact
preview text, including its scan state; it does not regenerate the report.
Display ordinals belong to that report, not Windows Identify or a stable device
identity. An open preview remains a snapshot if the underlying desk changes.

The CLI performs a fresh scan. It skips gamma-baseline resolution and never
loads app settings, saves baseline records, reapplies warmth or writes brightness
or contrast. Extra capability strings are not probed. It rejects all other
arguments except `--json` before enumerating, including monitor filters and
adjustments. A completed scan (even zero displays) exits 0; invalid arguments
exit 1 without a report; scan failure exits 2 with a failed report. JSON mode
keeps its JSON shape on scan failure and sends a fixed error notice to stderr.

## Schema 1

Text and JSON derive from the same `SupportReport.Document`. JSON keys use camel
case. The allowed root fields are:

| Field | Meaning |
|---|---|
| `schemaVersion` | Report schema, currently 1 |
| `appVersion` | Numeric assembly version, or `unknown` |
| `windowsVersion` | Numeric Windows version, or `unknown` |
| `scanState` | `completed`, `reading`, `failed` or `not checked` |
| `displays` | Current display records; empty unless the scan completed |

Each display includes only `number`, `brightnessAvailable`, `brightnessRange`,
`contrastAvailable`, `contrastRange`, `brightnessBackend`, `gammaRampReadable`
and `extraControlsScan`. Ranges contain `minimum` and `maximum`, and are null
when the corresponding control did not respond. Backend values are fixed
`DDC/CI`, `WMI` or `none or unknown`. Extra control scan is `completed` or
`not checked`; completed means the attempt finished, not that it succeeded or
exposed controls. Reading a gamma ramp does not establish that warmth can be
applied. Availability reflects observed reads, not future write success.

Names, EDID, serial numbers, stable keys, device paths, physical handles,
positions, resolutions, settings, raw capability strings, logs and exception
messages never enter the report object. Extend the projection deliberately
rather than serializing a Monitor or trying to redact arbitrary diagnostic text.
Schema field tests enforce that additions require an explicit test change.

`--diagnose` remains a separate, detailed local troubleshooting command. It
includes identifiers and raw diagnostic text; review and redact it before
sharing. Do not treat its output as the minimal report.

# Portable package and verification

`powershell -NoProfile -File scripts/build-release.ps1` publishes self-contained
single-file Windows x64 GUI and CLI binaries into a unique build directory.
Previous output is preserved. `scripts/package-release.ps1` assembles a new
package directory and calls `scripts/Test-Package.ps1` on the actual ZIP before
creating its archive checksum. It never publishes a release or changes GitHub.

The ZIP allowlist is exactly `LumenDeck.exe`, `lumendeck-cli.exe`, `LICENSE.txt`,
`QUICKSTART.txt` and `SHA256SUMS.txt`, all at the root. Source directories, PDBs,
local settings and generated MCP metadata do not enter the package. The manifest
records hashes of the four payload files. A separate `.zip.sha256` records the
ZIP hash after successful verification. Failed packages are renamed `.zip.failed`
and receive no release checksum. Hashes are integrity checks, not signatures.

The verifier checks entry names/counts before extraction, then extracted hashes,
both file versions and PE x64 headers. It runs the absolute extracted CLI with
`--help` and `--version`, and the GUI with `--verify-startup`. Each launched probe
has a 30-second timeout and cleanup targets only that probe. GUI verification
checks an embedded icon and renders production commands, connection guidance and
support-report controls in a non-activating off-desktop window. It bypasses the
normal singleton, startup registration, settings and monitor enumeration.
It also checks a reveal exchange on a unique test pipe with an inert callback;
the probe does not grant foreground permission or contact the normal app channel.

The synthetic probe also constructs and renders the brightness-profile editor
using a unique absent temporary path. It neither saves a profile nor reads the
user's profile file. It also renders the preset-target and preset-schedule
editors with built-in values and save delegates that refuse; no settings are
read or written and no clock-driven request is made.

The GUI probe also constructs and renders the Settings menu with an unavailable
startup state. It does not show the popup, read registry state or save preferences.

The process PATH contains only Windows System32, runtime root variables point
at a nonexistent directory, and native bundle extraction stays in the disposable
verification directory. All changed environment variables are restored in
`finally`, including launch-failure paths. This supports standalone packaging;
it does not replace execution on a separate Windows machine with no .NET runtime
installed. Synthetic startup also does not prove normal app startup, hardware
control, recovery or DPI behavior.

To test a produced archive:

```powershell
powershell -NoProfile -File test/Test-Package.ps1 -Archive <zip-path>
```

Regression cases cover a valid archive, extra contents, a changed payload,
version mismatch, an injected process-launch failure with environment restoration,
and installer refusal with an injected running app. The last checks do not
install/uninstall in the real user profile. Installer refusal is a preflight
check, not a transactional guarantee against every concurrent launch or failure.
Full install/uninstall acceptance remains a separate-profile manual check.

Before replacing installed files, the source installer checks that GUI and CLI
are regular Windows x64 files with matching FileVersion and ProductVersion.
This catches mixed outputs from skipped/interrupted builds; it does not verify
signatures or make installation transactional. Hardware-free source-preflight
tests exercise actual archive bytes and stop before directory creation. They
include a changed ProductVersion with the same release FileVersion, malformed
headers, missing metadata and recovery to a valid pair.

CI uses the same package builder and regression checks after its publish/version
steps, and uploads the verified ZIP plus archive checksum. Read hosted results
from the actual run; local success does not prove that hosted CI ran.

Before a public release, verify normal startup on a clean Windows profile,
complete the real-monitor checklist in `test/README.md`, check the unsigned
download experience, confirm the intended version and review publication scope.
No release is created automatically by these scripts.

Release compilation maps the physical repository root to `/_/` using
[PathMap](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/appconfig-compiler-option#pathmap).
This prevents checkout locations from entering compiler-emitted source/PDB paths.
Debug builds keep normal local paths. After a solution build,
`test/Test-ReleasePaths.ps1` checks all three product DLLs for mapped paths and
rejects the physical checkout prefix; CI uses the same check and injected
canaries. Package verification also calls `scripts/Test-BundleSourcePaths.ps1`
on each extracted EXE before launching probes. It reads the v6 bundle manifest
and decodes the application/Core assemblies in memory, then applies the same
source-path guard. Scanning only outer EXE bytes can miss compressed strings.

The marker lives inside the apphost. Its preceding Int64 stores the absolute
file offset of the manifest; the location of that Int64 is a separate value.
The reader rejects absent or ambiguous markers and out-of-file manifest offsets.
This follows the placeholder patch and offset read in the official .NET
[Bundler source](https://github.com/dotnet/runtime/blob/v10.0.0/src/installer/managed/Microsoft.NET.HostModel/Bundle/Bundler.cs).

The bundle check requires the expected application and Core assembly exactly
once, mapped source paths and no physical checkout prefix in either UTF-8 or
UTF-16 byte alignment. It bounds host size, entry counts, declared entry sizes
and decoded output; unsupported bundle versions fail. It neither executes nor
loads inspected assemblies, and uses only PowerShell/.NET built-ins. Its scope
is application compiler paths, not arbitrary secrets, other dependencies or
nested embedded PDB compression. Full publication-content review remains needed.

Use [the candidate-specific readiness checklist](release-readiness.md) to bind
source/version/archive identity to these checks and track remaining acceptance.


After recording a candidate, check its documentation identity against the archive:

```powershell
powershell -NoProfile -File scripts/Check-ReleaseRecord.ps1 -Archive <zip-path> -Gui <gui-exe-path> -Cli <cli-exe-path>
```

Use the two binaries under the same build's verified/files directory. The check
hashes each supplied EXE against its actual ZIP entry before comparing its
ProductVersion with the recorded source/build. It also verifies the ZIP digest
and adjacent .zip.sha256 record. No executable is started and no extraction is
performed. This complements package verification; it does not replace allowlist,
startup, independent review or release acceptance. Keep candidate identity only
in release-readiness.md to avoid multiple drifting receipts.

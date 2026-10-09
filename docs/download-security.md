# When Windows blocks a download

If Windows Security or another antivirus detects malware in a LumenDeck
download, stop. Leave protection enabled and follow the security product's
instructions. Do not restore a quarantined file, add an exclusion or run the
app to collect its version or diagnostics.

## Identify the warning without starting the app

A named malware detection, such as `Trojan:Win32/Wacatac.B!ml`, needs investigation
of that exact file. An unrecognized-app or unknown-publisher prompt concerns app
reputation or publisher identity. Microsoft documents the
[difference between Defender Antivirus and SmartScreen](https://learn.microsoft.com/en-us/defender-xdr/developer-faq)
and how [download reputation works](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).
LumenDeck downloads are currently unsigned. This does not establish why a
particular file was detected. Signing does not guarantee that a new download
will have no reputation warning.

If the file remains available, this PowerShell command reads its SHA-256 without
executing it and prints only the hash:

```powershell
(Get-FileHash -Algorithm SHA256 -LiteralPath '.\LumenDeck.exe').Hash
```

Use the actual downloaded filename; for a ZIP, hash the downloaded ZIP. If the
file is quarantined or unavailable, leave it there and skip this step. A matching
checksum confirms file identity, not that the file is safe.

## Report a blocked download

For an ordinary download-block report, include the release link, filename,
warning or detection name, Windows version and SHA-256 if available. You do not
need to run LumenDeck, generate diagnostics or upload the executable to a public
issue. Review screenshots first: file paths, account names and security history
can reveal private information. Use the [private security channel](../SECURITY.md)
for a suspected vulnerability or sensitive evidence.

As of 2026-10-08, [issue #5](https://github.com/snowyukitty/LumenDeck/issues/5)
remains open. It reports a Wacatac detection for `LumenDeck.exe` with version
`1.1.0.0` on Windows 11. No file hash is included, so that report cannot identify
an exact release asset or establish the status of a later build. The linked
Microsoft submission currently has no available details. There is no verified
final determination here; the report is not being labeled a false positive.

## Maintainer follow-up

Bind an investigation to the exact download URL, file hash and build, then record
the security product, engine/intelligence versions, observation date and result.
Keep device-specific evidence private. Local build tests and checksum checks do
not resolve an antivirus report, and a result for one hash does not transfer to
a new build.

Before an automated scan, review the security product's cloud/sample-submission
behavior. A local command can still trigger an upload under the device's existing
policy. Microsoft's [cloud-protection configuration guide](https://learn.microsoft.com/en-us/defender-endpoint/cloud-protection-configure)
documents automatic sample submission. Obtain owner approval for any upload;
do not change protection settings to make a test run.

For `MpCmdRun`, exit code `0` alone does not establish that nothing was detected:
Microsoft documents both no detection and successful remediation under that code.
Code `2` can indicate an unremediated detection, required user action or a scan
error. Preserve the command's detection/error output and file identity alongside
the exit code. The custom-scan `-DisableRemediation` option changes detection
handling; it does not document an offline or no-upload guarantee. See the
[command reference](https://learn.microsoft.com/en-us/defender-endpoint/command-line-arguments-microsoft-defender-antivirus).

Microsoft's [submission guide](https://learn.microsoft.com/en-us/defender-xdr/submission-guide)
describes submitting the exact file as a software developer and waiting for a
final determination. Any submission or file upload needs the owner's approval;
this guide does not send anything. A new release still needs its own download
and security acceptance. Do not close the existing report or claim it is fixed
solely because the source was rebuilt or signed.

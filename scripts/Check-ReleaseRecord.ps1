param(
    [Parameter(Mandatory = $true)][string]$Archive,
    [Parameter(Mandatory = $true)][string]$Gui,
    [Parameter(Mandatory = $true)][string]$Cli,
    [string]$Record
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
if (!$Record) { $Record = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/release-readiness.md' }

function Read-RecordCell([string]$Text, [string]$Name) {
    $pattern = '(?m)^\| ' + [regex]::Escape($Name) + ' \| ([^\r\n|]+) \|\r?$'
    $cells = [regex]::Matches($Text, $pattern)
    if ($cells.Count -ne 1) { throw "Expected exactly one recorded $Name cell." }
    return $cells[0].Groups[1].Value.Trim()
}

function Get-StreamHash([System.IO.Stream]$Stream) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Stream))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

$text = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $Record).Path)
$source = [regex]::Match((Read-RecordCell $text 'Source commit'), '^`([0-9a-f]{40})`(?: |$)')
$build = [regex]::Match((Read-RecordCell $text 'GUI recorded build'), '^`(\d+\.\d+\.\d+\+([0-9a-f]{40}))`$')
$digest = [regex]::Match((Read-RecordCell $text 'SHA-256'), '^`([0-9a-f]{64})`$')
$package = [regex]::Match((Read-RecordCell $text 'Package'), '^`([^`/\\]+\.zip)`$')
if (!$source.Success -or !$build.Success -or !$digest.Success -or !$package.Success) { throw 'Malformed candidate identity.' }
if ($source.Groups[1].Value -ne $build.Groups[2].Value) { throw 'Recorded source and GUI build disagree.' }
if ([System.IO.Path]::GetFileName($Archive) -cne $package.Groups[1].Value) { throw 'Archive name disagrees with the record.' }

$archivePath = (Resolve-Path -LiteralPath $Archive).Path
$archiveStream = [System.IO.File]::Open($archivePath, 'Open', 'Read', 'Read')
try {
    $actualDigest = Get-StreamHash $archiveStream
    if ($actualDigest -ne $digest.Groups[1].Value) { throw 'Archive SHA-256 disagrees with the record.' }
    $checksum = [System.IO.File]::ReadAllText($archivePath + '.sha256').Trim()
    if ($checksum -cne ($actualDigest + '  ' + $package.Groups[1].Value)) { throw 'Archive checksum record disagrees.' }
    $archiveStream.Position = 0
    $zip = [System.IO.Compression.ZipArchive]::new($archiveStream, 'Read', $true)
    try {
        foreach ($binary in @(@('LumenDeck.exe', $Gui), @('lumendeck-cli.exe', $Cli))) {
            $entries = @($zip.Entries | Where-Object { $_.FullName -ceq $binary[0] })
            if ($entries.Count -ne 1) { throw 'Expected one root entry for each executable.' }
            $binaryPath = (Resolve-Path -LiteralPath $binary[1]).Path
            $binaryStream = [System.IO.File]::Open($binaryPath, 'Open', 'Read', 'Read')
            try {
                $entryStream = $entries[0].Open()
                try { $entryHash = Get-StreamHash $entryStream }
                finally { $entryStream.Dispose() }
                if ((Get-StreamHash $binaryStream) -ne $entryHash) { throw 'Supplied executable differs from the archive payload.' }
                $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($binaryPath)
                if ($version.ProductVersion -cne $build.Groups[1].Value) { throw 'Executable ProductVersion disagrees with the record.' }
            }
            finally { $binaryStream.Dispose() }
        }
    }
    finally { $zip.Dispose() }
}
finally { $archiveStream.Dispose() }
Write-Output 'PASS: recorded source/build, both archived executable identities, ZIP digest and checksum record agree.'

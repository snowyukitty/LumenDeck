param(
    [Parameter(Mandatory = $true)][string]$Archive,
    [Parameter(Mandatory = $true)][string]$ExpectedVersion
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payload = @('LumenDeck.exe', 'lumendeck-cli.exe', 'LICENSE.txt', 'QUICKSTART.txt')
$allowed = @($payload + 'SHA256SUMS.txt')
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive).Path)
try {
    $names = @($zip.Entries | ForEach-Object { $_.FullName })
    if ($names.Count -ne $allowed.Count) { throw 'Unexpected archive entry count.' }
    foreach ($name in $allowed) {
        if (@($names | Where-Object { $_ -ceq $name }).Count -ne 1) { throw 'Archive payload does not match the allowlist.' }
    }
} finally { $zip.Dispose() }

$tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$sandbox = [IO.Path]::GetFullPath((Join-Path $tempRoot ('LumenDeck-package-' + [guid]::NewGuid().ToString('N'))))
$savedEnvironment = @{}
foreach ($name in @('PATH', 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_MULTILEVEL_LOOKUP', 'DOTNET_BUNDLE_EXTRACT_BASE_DIR')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    [IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $Archive).Path, $sandbox)
    $seen = @{}
    foreach ($line in Get-Content -LiteralPath (Join-Path $sandbox 'SHA256SUMS.txt')) {
        if ($line -cnotmatch '^([0-9a-f]{64})  (.+)$') { throw 'Invalid checksum manifest line.' }
        $digest = $matches[1]; $name = $matches[2]
        if ($payload -cnotcontains $name -or $seen.ContainsKey($name)) { throw 'Unexpected or duplicate checksum entry.' }
        $seen[$name] = $true
        if ((Get-FileHash -LiteralPath (Join-Path $sandbox $name) -Algorithm SHA256).Hash.ToLowerInvariant() -cne $digest) {
            throw 'Extracted payload failed checksum verification.'
        }
    }
    if ($seen.Count -ne $payload.Count) { throw 'Incomplete checksum manifest.' }
    foreach ($name in @('LumenDeck.exe', 'lumendeck-cli.exe')) {
        $exe = Join-Path $sandbox $name
        if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne "$ExpectedVersion.0") {
            throw 'Executable file version does not match the package version.'
        }
        $reader = New-Object IO.BinaryReader([IO.File]::OpenRead($exe))
        try {
            if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'Missing executable header.' }
            $reader.BaseStream.Position = 0x3C
            $reader.BaseStream.Position = $reader.ReadInt32()
            if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne 0x8664) {
                throw 'Package requires Windows x64 executables.'
            }
        } finally { $reader.Dispose() }
    }
    & (Join-Path $PSScriptRoot 'Test-BundleSourcePaths.ps1') -Executable (Join-Path $sandbox 'LumenDeck.exe') -ApplicationAssembly 'LumenDeck.dll'
    & (Join-Path $PSScriptRoot 'Test-BundleSourcePaths.ps1') -Executable (Join-Path $sandbox 'lumendeck-cli.exe') -ApplicationAssembly 'lumendeck-cli.dll'
    $env:PATH = Join-Path $env:SystemRoot 'System32'
    $env:DOTNET_ROOT = Join-Path $sandbox 'no-dotnet'
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $sandbox 'bundle'
    $cli = Join-Path $sandbox 'lumendeck-cli.exe'
    foreach ($argument in @('--version', '--help')) {
        $stdout = Join-Path $sandbox 'cli-output.txt'
        $stderr = Join-Path $sandbox 'cli-error.txt'
        $process = Start-Process -FilePath $cli -ArgumentList $argument -WorkingDirectory $sandbox `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Hidden -PassThru
        $null = $process.Handle
        if (-not $process.WaitForExit(30000)) {
            $process.Kill(); $process.WaitForExit()
            throw 'Extracted CLI probe timed out.'
        }
        $process.Refresh()
        if ($process.ExitCode -ne 0) { throw ('Extracted CLI probe failed with exit ' + $process.ExitCode) }
        $output = Get-Content -LiteralPath $stdout -Raw
        if ($argument -eq '--version' -and $output.Trim() -ne $ExpectedVersion) { throw 'Extracted CLI version check failed.' }
        if ($argument -eq '--help' -and $output -notmatch '--support-report') { throw 'Extracted CLI help is incomplete.' }
    }
    $gui = Start-Process -FilePath (Join-Path $sandbox 'LumenDeck.exe') -ArgumentList '--verify-startup' `
        -WorkingDirectory $sandbox -WindowStyle Hidden -PassThru
    $null = $gui.Handle
    if (-not $gui.WaitForExit(30000)) {
        # Only this synthetic probe process is stopped; never a user's app.
        $gui.Kill()
        $gui.WaitForExit()
        throw 'Extracted GUI startup probe timed out.'
    }
    $gui.Refresh()
    if ($gui.ExitCode -ne 0) { throw ('Extracted GUI startup probe failed with exit ' + $gui.ExitCode) }
    Write-Host 'PASS: archive allowlist, hashes, versions, standalone CLI and synthetic GUI startup.'
} finally {
    foreach ($name in $savedEnvironment.Keys) {
        if ($null -eq $savedEnvironment[$name]) {
            # PowerShell method binding can turn null into an empty string on
            # modern .NET, where empty is a real value rather than deletion.
            Remove-Item -LiteralPath ('Env:\' + $name) -ErrorAction SilentlyContinue
        } else {
            [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
        }
    }
    # Verify the absolute cleanup boundary before recursive removal.
    if (-not $sandbox.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $sandbox) -notlike 'LumenDeck-package-*') { throw 'Unsafe package verification cleanup path.' }
    if (Test-Path -LiteralPath $sandbox) { Remove-Item -LiteralPath $sandbox -Recurse -Force }
}

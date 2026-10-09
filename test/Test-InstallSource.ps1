param([Parameter(Mandatory = $true)][string]$Archive)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$tempBoundary = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$work = Join-Path $tempBoundary ('LumenDeck-install-source-' + [guid]::NewGuid().ToString('N'))
$savedLocal = $env:LOCALAPPDATA
$savedRoaming = $env:APPDATA
try {
    $null = New-Item -ItemType Directory -Path $work
    $files = Join-Path $work 'payload'
    Expand-Archive -LiteralPath $Archive -DestinationPath $files
    Copy-Item -LiteralPath (Join-Path $repo 'install.ps1') -Destination $work
    $guiDir = Join-Path $work 'dist\gui'
    $cliDir = Join-Path $work 'dist\cli'
    $null = New-Item -ItemType Directory -Path $guiDir, $cliDir
    Copy-Item -LiteralPath (Join-Path $files 'LumenDeck.exe') -Destination $guiDir
    $cli = Join-Path $cliDir 'lumendeck-cli.exe'
    $originalCli = Join-Path $files 'lumendeck-cli.exe'
    $env:LOCALAPPDATA = Join-Path $work 'local'
    $env:APPDATA = Join-Path $work 'roaming'

    function Assert-InstallStops([string]$expected) {
        # The real script and actual PE/version bytes are exercised. Stop at
        # its first installation mutation; never install or launch a binary.
        $message = & {
            function Get-Process { param($Name, $ErrorAction) }
            function New-Item { throw 'Reached installation boundary.' }
            function Copy-Item { throw 'Unexpected copy mutation.' }
            function Remove-Item { throw 'Unexpected remove mutation.' }
            try { & (Join-Path $work 'install.ps1') -SkipBuild }
            catch { return $_.Exception.Message }
            throw 'Installer unexpectedly returned.'
        }
        if ($message -cne $expected) { throw "Expected '$expected'; got '$message'." }
        if ((Test-Path -LiteralPath $env:LOCALAPPDATA) -or (Test-Path -LiteralPath $env:APPDATA)) {
            throw 'Installer changed an isolated user-state directory.'
        }
        Write-Host ('PASS: ' + $expected)
    }

    Copy-Item -LiteralPath $originalCli -Destination $cli
    Assert-InstallStops 'Reached installation boundary.'
    Remove-Item -LiteralPath $cli
    Assert-InstallStops ('Missing build output: ' + $cli)
    [IO.File]::WriteAllBytes($cli, [byte[]]@(0x4D, 0x5A))
    Assert-InstallStops 'Install source requires a valid Windows x64 executable.'

    $headerOnly = New-Object byte[] 70
    $headerOnly[0] = 0x4D; $headerOnly[1] = 0x5A
    $headerOnly[0x3C] = 64
    $headerOnly[64] = 0x50; $headerOnly[65] = 0x45
    $headerOnly[68] = 0x64; $headerOnly[69] = 0x86
    [IO.File]::WriteAllBytes($cli, $headerOnly)
    Assert-InstallStops 'Install source is missing version metadata.'

    $bytes = [IO.File]::ReadAllBytes($originalCli)
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
    $bytes[$peOffset + 4] = 0x4C
    $bytes[$peOffset + 5] = 0x01
    [IO.File]::WriteAllBytes($cli, $bytes)
    Assert-InstallStops 'Install source requires a valid Windows x64 executable.'

    $bytes = [IO.File]::ReadAllBytes($originalCli)
    [Array]::Copy([BitConverter]::GetBytes([int]::MaxValue), 0, $bytes, 0x3C, 4)
    [IO.File]::WriteAllBytes($cli, $bytes)
    Assert-InstallStops 'Install source requires a valid Windows x64 executable.'

    # Change a real resource string, retaining a readable native PE and the
    # same release FileVersion. This models different builds of one version.
    $bytes = [IO.File]::ReadAllBytes($originalCli)
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($originalCli).ProductVersion
    $resourceText = [Text.Encoding]::Unicode.GetString($bytes)
    $index = $resourceText.IndexOf($version, [StringComparison]::Ordinal)
    if ($index -lt 0) { throw 'Version resource fixture was not found.' }
    $replacement = $version.Substring(0, $version.Length - 1) + $(if ($version.EndsWith('0')) { '1' } else { '0' })
    $replacementBytes = [Text.Encoding]::Unicode.GetBytes($replacement)
    [Array]::Copy($replacementBytes, 0, $bytes, $index * 2, $replacementBytes.Length)
    [IO.File]::WriteAllBytes($cli, $bytes)
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($cli).ProductVersion -cne $replacement) {
        throw 'Changed version resource fixture was not observed.'
    }
    Assert-InstallStops 'GUI and CLI build versions differ. Rebuild both before installing.'

    Copy-Item -LiteralPath $originalCli -Destination $cli -Force
    Assert-InstallStops 'Reached installation boundary.'
} finally {
    $env:LOCALAPPDATA = $savedLocal
    $env:APPDATA = $savedRoaming
    $absoluteWork = [IO.Path]::GetFullPath($work)
    if (-not $absoluteWork.StartsWith($tempBoundary, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $absoluteWork) -notlike 'LumenDeck-install-source-*') {
        throw 'Unsafe installer test cleanup path.'
    }
    if (Test-Path -LiteralPath $absoluteWork) { Remove-Item -LiteralPath $absoluteWork -Recurse -Force }
}

# install.ps1 - build LumenDeck and put it somewhere it can actually be used.
#
# Installs to %LOCALAPPDATA%\Programs\LumenDeck, which is the per-user location
# Windows itself uses for apps that need no elevation. Deliberately NOT the
# build output directory: bin\ and dist\ are wiped by the next clean build, and
# a Start Menu shortcut pointing into one of them breaks silently the first time
# somebody rebuilds.
#
# Adds the install directory to the user PATH so `lumendeck-cli` works from any
# shell, and creates a Start Menu shortcut for the app.
#
# No elevation required. -Uninstall reverses everything it did.
#
# Usage:
#   powershell -File install.ps1
#   powershell -File install.ps1 -Desktop
#   powershell -File install.ps1 -Uninstall

param(
    [switch]$Desktop,
    [switch]$Uninstall,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$repo = $PSScriptRoot
$target = Join-Path $env:LOCALAPPDATA 'Programs\LumenDeck'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcut = Join-Path $startMenu 'LumenDeck.lnk'
$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'LumenDeck.lnk'
if ((Test-Path -LiteralPath $target) -and
    ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Refusing to change files through a redirected app directory.'
}

# Force termination bypasses the app's restore path for temporary blackouts.
# Refuse before changing files, shortcuts, PATH or the startup entry instead.
if (Get-Process LumenDeck -ErrorAction SilentlyContinue) {
    throw 'Exit LumenDeck from its tray menu before installing or uninstalling. This lets it restore blanked screens safely.'
}

function Update-UserPathValue($Key, [string]$dir, [switch]$Remove) {
    # Keep expandable registry text and its value kind intact. Environment's
    # User setter rewrites REG_EXPAND_SZ as REG_SZ on .NET Framework.
    $current = $Key.GetValue('Path', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    if ($null -ne $current -and $current -isnot [string]) { throw 'User PATH is not a string value.' }
    $kind = if ($null -eq $current) { [Microsoft.Win32.RegistryValueKind]::ExpandString } else { $Key.GetValueKind('Path') }
    if ($kind -notin @([Microsoft.Win32.RegistryValueKind]::String, [Microsoft.Win32.RegistryValueKind]::ExpandString)) {
        throw 'User PATH has an unsupported registry value kind.'
    }
    $parts = if ($null -eq $current) { @() } else { @($current -split ';') }
    $kept = @($parts | Where-Object { $_.TrimEnd('\') -ine $dir.TrimEnd('\') })
    $found = $kept.Count -ne $parts.Count
    if (($Remove -and -not $found) -or (-not $Remove -and $found)) { return $false }
    if ($Remove) { $updated = $kept -join ';' }
    elseif ([string]::IsNullOrEmpty($current)) { $updated = $dir }
    else { $updated = $current + ';' + $dir }
    $Key.SetValue('Path', $updated, $kind)
    return $true
}

function Update-UserPath([string]$dir, [switch]$Remove) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
    if ($null -eq $key) {
        if ($Remove) { return $false }
        $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Environment')
    }
    try { $changed = Update-UserPathValue $key $dir -Remove:$Remove }
    finally { $key.Dispose() }
    if ($changed) {
        if (-not ('LumenDeck.InstallEnvironment' -as [type])) {
            Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace LumenDeck {
    public static class InstallEnvironment {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr window, uint message,
            UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);
    }
}
'@
        }
        $result = [UIntPtr]::Zero
        $null = [LumenDeck.InstallEnvironment]::SendMessageTimeout([IntPtr]0xffff, 0x1a,
            [UIntPtr]::Zero, 'Environment', 2, 1000, [ref]$result)
    }
    return $changed
}

# ------------------------------------------------------------------ uninstall
if ($Uninstall) {
    $expectedTarget = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\LumenDeck'))
    if ([IO.Path]::GetFullPath($target) -ne $expectedTarget) { throw 'Unexpected uninstall target.' }
    if (Test-Path -LiteralPath $target) {
        foreach ($name in @('LumenDeck.exe', 'lumendeck-cli.exe')) {
            $ownedFile = Join-Path $target $name
            if (Test-Path -LiteralPath $ownedFile) { Remove-Item -LiteralPath $ownedFile -Force }
        }
        if (@(Get-ChildItem -LiteralPath $target -Force).Count -eq 0) {
            Remove-Item -LiteralPath $target -Force
        } else { Write-Host 'Other files in the app directory were preserved.' }
    }
    # Only remove launch entries after owned files were removed successfully.
    # A file sharing/permission error must not first erase the user's entries.
    foreach ($p in @($shortcut, $desktopLink)) {
        if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force; Write-Host ('removed ' + $p) }
    }
    $run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    if ((Get-ItemProperty -Path $run -ErrorAction SilentlyContinue).PSObject.Properties.Name -contains 'LumenDeck') {
        Remove-ItemProperty -Path $run -Name 'LumenDeck' -Force
        Write-Host 'removed the start-with-Windows entry'
    }
    $null = Update-UserPath $target -Remove
    Write-Host ''
    Write-Host 'Uninstalled. Settings in %APPDATA%\LumenDeck were left alone - delete that folder to remove them too.'
    return
}

# ---------------------------------------------------------------------- build
if (-not $SkipBuild) {
    $dotnet = 'dotnet'
    if (-not (Get-Command $dotnet -ErrorAction SilentlyContinue)) {
        $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
        if (-not (Test-Path $dotnet)) { throw 'dotnet SDK not found. Install the .NET 10 SDK, or pass -SkipBuild.' }
    }

    Write-Host 'Publishing self-contained binaries...'
    # Separate output folders on purpose. Publishing both into one directory
    # makes the second publish clean out the first one's executable.
    & $dotnet publish (Join-Path $repo 'src\LumenDeck\LumenDeck.csproj') -c Release -r win-x64 `
        --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -o (Join-Path $repo 'dist\gui') -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'GUI publish failed.' }

    & $dotnet publish (Join-Path $repo 'src\LumenDeck.Cli\LumenDeck.Cli.csproj') -c Release -r win-x64 `
        --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -o (Join-Path $repo 'dist\cli') -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'CLI publish failed.' }
}

$gui = Join-Path $repo 'dist\gui\LumenDeck.exe'
$cli = Join-Path $repo 'dist\cli\lumendeck-cli.exe'
foreach ($p in @($gui, $cli)) { if (-not (Test-Path $p)) { throw ('Missing build output: ' + $p) } }

# A skipped or interrupted build can leave a mixed pair. Reject it before
# changing installed files, shortcuts or user PATH. This is not signature trust.
$sourceVersions = @()
foreach ($p in @($gui, $cli)) {
    $sourceFile = Get-Item -LiteralPath $p
    if ($sourceFile.PSIsContainer -or ($sourceFile.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Install source must be a regular executable file.'
    }
    $reader = New-Object IO.BinaryReader([IO.File]::OpenRead($p))
    try {
        if ($reader.BaseStream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5A4D) {
            throw 'Install source requires a valid Windows x64 executable.'
        }
        $reader.BaseStream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 64 -or $peOffset -gt ($reader.BaseStream.Length - 6)) {
            throw 'Install source requires a valid Windows x64 executable.'
        }
        $reader.BaseStream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne 0x8664) {
            throw 'Install source requires a valid Windows x64 executable.'
        }
    } finally { $reader.Dispose() }
    $version = $sourceFile.VersionInfo
    if ([string]::IsNullOrWhiteSpace($version.FileVersion) -or
        [string]::IsNullOrWhiteSpace($version.ProductVersion)) {
        throw 'Install source is missing version metadata.'
    }
    $sourceVersions += $version
}
if ($sourceVersions[0].FileVersion -cne $sourceVersions[1].FileVersion -or
    $sourceVersions[0].ProductVersion -cne $sourceVersions[1].ProductVersion) {
    throw 'GUI and CLI build versions differ. Rebuild both before installing.'
}

# -------------------------------------------------------------------- install
if (Get-Process LumenDeck -ErrorAction SilentlyContinue) {
    throw 'LumenDeck started during the build. Exit it from its tray menu, then retry with -SkipBuild.'
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item $gui $target -Force
Copy-Item $cli $target -Force
Write-Host ('installed to ' + $target)

# ------------------------------------------------------------------- shortcut
$shell = New-Object -ComObject WScript.Shell
foreach ($link in @($shortcut, $(if ($Desktop) { $desktopLink }))) {
    if (-not $link) { continue }
    $sc = $shell.CreateShortcut($link)
    $sc.TargetPath = Join-Path $target 'LumenDeck.exe'
    $sc.WorkingDirectory = $target
    $sc.Description = 'Brightness, contrast and colour temperature for every monitor'
    $sc.IconLocation = (Join-Path $target 'LumenDeck.exe') + ',0'
    $sc.Save()
    Write-Host ('shortcut  ' + $link)
}

# ----------------------------------------------------------------- user PATH
if (Update-UserPath $target) {
    Write-Host ('added to user PATH: ' + $target)
    Write-Host '  (open a NEW shell before `lumendeck-cli` resolves - the current one has the old PATH)'
} else {
    Write-Host 'already on the user PATH'
}

# -------------------------------------------------------------------- verify
Write-Host ''
Write-Host 'Verifying...'
$installedCli = Join-Path $target 'lumendeck-cli.exe'
& $installedCli --version | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'The installed CLI did not run.' }
Write-Host '  CLI runs'

& $installedCli --help | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'The installed CLI help did not run.' }
Write-Host '  CLI help runs; monitor connection checks are available in the app'

Write-Host ''
Write-Host 'Done. Launch LumenDeck from the Start Menu; it lives in the notification area.'
Write-Host 'Turn on "Start with Windows" from its tray menu if you want warmth reapplied at logon.'
Write-Host ''
Write-Host 'Undo everything with:  powershell -File install.ps1 -Uninstall'

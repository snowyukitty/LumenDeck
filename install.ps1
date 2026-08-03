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

function Remove-FromUserPath([string]$dir) {
    $current = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (-not $current) { return }
    $kept = @($current -split ';' | Where-Object { $_ -and ($_.TrimEnd('\') -ne $dir.TrimEnd('\')) })
    [Environment]::SetEnvironmentVariable('Path', ($kept -join ';'), 'User')
}

# ------------------------------------------------------------------ uninstall
if ($Uninstall) {
    foreach ($p in @($shortcut, $desktopLink)) {
        if (Test-Path $p) { Remove-Item $p -Force; Write-Host ('removed ' + $p) }
    }

    # Autostart is registered by the app itself, under HKCU Run. Clear it here
    # too, or uninstalling leaves Windows trying to launch a deleted binary at
    # every logon.
    $run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    if ((Get-ItemProperty -Path $run -ErrorAction SilentlyContinue).PSObject.Properties.Name -contains 'LumenDeck') {
        Remove-ItemProperty -Path $run -Name 'LumenDeck' -Force
        Write-Host 'removed the start-with-Windows entry'
    }

    Get-Process LumenDeck -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500

    if (Test-Path $target) { Remove-Item $target -Recurse -Force; Write-Host ('removed ' + $target) }
    Remove-FromUserPath $target
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

# -------------------------------------------------------------------- install
Get-Process LumenDeck -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

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
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$already = $userPath -and (@($userPath -split ';' | ForEach-Object { $_.TrimEnd('\') }) -contains $target.TrimEnd('\'))
if (-not $already) {
    $joined = if ([string]::IsNullOrEmpty($userPath)) { $target } else { $userPath.TrimEnd(';') + ';' + $target }
    [Environment]::SetEnvironmentVariable('Path', $joined, 'User')
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

$monitors = (& $installedCli --list --json | ConvertFrom-Json)
Write-Host ('  ' + @($monitors).Count + ' monitor(s) detected, ' +
            @($monitors | Where-Object { $_.backend -ne 'None' }).Count + ' controllable')

Write-Host ''
Write-Host 'Done. Launch LumenDeck from the Start Menu; it lives in the notification area.'
Write-Host 'Turn on "Start with Windows" from its tray menu if you want warmth reapplied at logon.'
Write-Host ''
Write-Host 'Undo everything with:  powershell -File install.ps1 -Uninstall'

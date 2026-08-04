# Test-Startup.ps1
# Three things the app must get right at launch.
#
# 1. TIME TO WINDOW. The first version enumerated every monitor over DDC inside
#    the form constructor, so nothing appeared for 6.72 s after a double-click -
#    measured, not guessed. That reads as a hung app. Enumeration now runs off
#    the UI thread from OnShown, so the window should appear immediately.
#
# 2. SURVIVING A BROKEN SETTINGS FILE. `{"Monitors": null}` is valid JSON, so it
#    deserialises without throwing and only blows up on first use - inside the
#    constructor, before the message loop exists, where Application.ThreadException
#    cannot catch it. The app would simply vanish at launch with no window and no
#    error dialog. Every case below must still produce a window.
#
# 3. STARTING MINIMISED. The one launch path with no window to wait for, and it
#    used to kill the process: exit code 0xC00000FD, STACK_OVERFLOW, with no
#    window, no tray icon and no error.log. See the case at the bottom.
#
# Restores the real settings file afterwards.
#
# Usage: powershell -File Test-Startup.ps1

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot   # repository root
$exe = Join-Path $root 'src\LumenDeck\bin\Release\net10.0-windows\LumenDeck.exe'
if (-not (Test-Path $exe)) { throw ('Build it first - not found: ' + $exe) }

$dir = Join-Path $env:APPDATA 'LumenDeck'
$settings = Join-Path $dir 'settings.json'
$errlog = Join-Path $dir 'error.log'
$backup = Join-Path $env:TEMP ('displaycontrol-settings-backup-' + (Get-Date -Format 'yyyyMMddHHmmss') + '.json')

$hadSettings = Test-Path $settings
if ($hadSettings) { Copy-Item $settings $backup -Force }

function Start-AndWaitForWindow {
    param([int]$TimeoutSec = 30)

    Get-Process LumenDeck -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 700
    if (Test-Path $errlog) { Remove-Item $errlog -Force }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process $exe -PassThru
    while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
        Start-Sleep -Milliseconds 100
        $p.Refresh()
        if ($p.HasExited) {
            $sw.Stop()
            return [pscustomobject]@{ Ok = $false; Seconds = $sw.Elapsed.TotalSeconds; Why = 'process exited, code ' + $p.ExitCode }
        }
        if ($p.MainWindowHandle -ne 0) {
            $sw.Stop()
            return [pscustomobject]@{ Ok = $true; Seconds = $sw.Elapsed.TotalSeconds; Why = '' }
        }
    }
    $sw.Stop()
    return [pscustomobject]@{ Ok = $false; Seconds = $sw.Elapsed.TotalSeconds; Why = 'no window within timeout' }
}

$fail = 0

# ---------------------------------------------------------------- 1. latency
Write-Host ''
Write-Host '=== time to window, normal settings ==='
if (Test-Path $settings) { Remove-Item $settings -Force }

# One launch thrown away first. The very first run of a freshly built executable
# pays for JIT and for paging a large binary in off disk: measured at 2.97 s here
# against 0.41 s on every run after it. That is a failure every time somebody
# builds and immediately tests, which is when everybody tests - and it is not the
# thing this case is about. The question is whether the DDC enumeration still
# blocks the window, and that cost does not disappear when the binary is warm.
[void](Start-AndWaitForWindow)

$r = Start-AndWaitForWindow
if (-not $r.Ok) {
    Write-Host ('  FAIL - ' + $r.Why)
    $fail++
} else {
    $t = [math]::Round($r.Seconds, 2)
    Write-Host ('  window after ' + $t + ' s')
    # The enumeration itself still takes seconds; the point is that it no longer
    # happens before the window exists.
    if ($r.Seconds -gt 2.5) { Write-Host '  FAIL - startup still blocks on the DDC enumeration'; $fail++ }
    else { Write-Host '  PASS' }
}

# -------------------------------------------------- 2. broken settings files
$cases = @(
    @{ Name = 'Monitors is null';        Json = '{"Monitors": null}' },
    @{ Name = 'null inside the array';   Json = '{"Monitors": [null]}' },
    @{ Name = 'entry with null Key';     Json = '{"Monitors": [{"Key": null, "Kelvin": 5000}]}' },
    @{ Name = 'absurd Kelvin';           Json = '{"Monitors": [{"Key": "x", "Kelvin": -999999}]}' },
    @{ Name = 'not JSON at all';         Json = 'this is not json {{{' },
    @{ Name = 'empty file';              Json = '' }
)

foreach ($c in $cases) {
    Write-Host ''
    Write-Host ('=== settings: ' + $c.Name + ' ===')
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content -Path $settings -Value $c.Json -Encoding UTF8 -NoNewline

    $r = Start-AndWaitForWindow
    if ($r.Ok) {
        Write-Host ('  PASS - window after ' + [math]::Round($r.Seconds, 2) + ' s')
    } else {
        Write-Host ('  FAIL - ' + $r.Why)
        if (Test-Path $errlog) { Write-Host '  --- error.log ---'; Get-Content $errlog | ForEach-Object { Write-Host ('  ' + $_) } }
        $fail++
    }
}

# ------------------------------------------------------ 3. starting minimised
#
# The only launch path with nothing to wait for, which is exactly why it went
# unnoticed. Assigning WindowState in the form constructor put the first WM_SIZE
# before the window had ever been shown; the minimise-to-tray path reacted by
# assigning ShowInTaskbar, which recreates the window handle, which resizes the
# form, which re-entered that path with the state still Minimized. Round it went
# until the stack was gone.
#
# So this asserts the two things that were false: the process is still alive,
# and it got as far as reading the monitors. The second is visible in the
# settings file - the first rebuild seeds a Custom entry per monitor, so a file
# that goes in with an empty Monitors list comes out with one entry per screen.
Write-Host ''
Write-Host '=== starting minimised ==='
New-Item -ItemType Directory -Path $dir -Force | Out-Null
Set-Content -Path $settings -Encoding UTF8 `
    -Value '{"Monitors": [], "StartMinimised": true, "MinimiseToTray": true}'

Get-Process LumenDeck -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700
if (Test-Path $errlog) { Remove-Item $errlog -Force }

$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 20
$p.Refresh()

if ($p.HasExited) {
    Write-Host ('  FAIL - process exited, code 0x{0:X8}' -f $p.ExitCode)
    if ($p.ExitCode -eq -1073741571) { Write-Host '         that is STACK_OVERFLOW - the minimise-at-launch recursion is back' }
    if (Test-Path $errlog) { Write-Host '  --- error.log ---'; Get-Content $errlog | ForEach-Object { Write-Host ('  ' + $_) } }
    $fail++
} else {
    $seen = 0
    try { $seen = @((Get-Content $settings -Raw | ConvertFrom-Json).Monitors).Count } catch { $seen = 0 }

    if ($p.MainWindowHandle -ne 0) {
        Write-Host '  FAIL - a window was shown; StartMinimised should go straight to the tray'
        $fail++
    } elseif ($seen -eq 0) {
        Write-Host '  FAIL - alive, but it never enumerated: no monitor reached the settings file'
        $fail++
    } else {
        Write-Host ('  PASS - alive in the tray, no window, ' + $seen + ' monitor(s) enumerated')
    }
}

# ------------------------------------------------------------------ teardown
Get-Process LumenDeck -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if ($hadSettings) {
    Copy-Item $backup $settings -Force
    Write-Host ''
    Write-Host ('Restored the original settings file from ' + $backup)
} elseif (Test-Path $settings) {
    Remove-Item $settings -Force
    Write-Host ''
    Write-Host 'Removed the test settings file (there was none before).'
}

Write-Host ''
if ($fail -eq 0) { Write-Host 'ALL PASS'; exit 0 }
Write-Host ([string]$fail + ' FAILED')
exit 1

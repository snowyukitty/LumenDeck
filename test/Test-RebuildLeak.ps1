# Test-RebuildLeak.ps1
# Does repeatedly rebuilding the monitor list leak GDI/USER objects?
#
# The first version of MainForm.Rebuild called _list.Controls.Clear(), which
# REMOVES controls without disposing them. Every rebuild would therefore leak
# the panels' Fonts, Labels, TrackBars and window handles - once per Refresh
# click and once per display change. A slow leak like that never shows up in a
# quick manual test; it shows up as a mysteriously sluggish app hours later.
#
# WM_DISPLAYCHANGE drives the same code path as the Refresh button, and can be
# posted from outside the process, so this exercises it without a UI robot.
#
# GetGuiResources is the right instrument here: the plain HandleCount property
# counts kernel handles and barely moves when GDI objects leak.
#
# Usage: powershell -File Test-RebuildLeak.ps1 [-Cycles 8]

param(
    [int]$Cycles = 8,
    # WM_DISPLAYCHANGE is debounced for 900 ms before enumeration even starts.
    # Four real DDC monitors take several seconds after that; sampling sooner
    # measures alternating empty/half-built/full UI states and reports the
    # oscillation as a leak. 6500 ms is the measured complete-rebuild interval
    # on the four-monitor development desk.
    [int]$SettleMs = 6500
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot   # repository root
$exe = Join-Path $root 'src\LumenDeck\bin\Release\net10.0-windows\LumenDeck.exe'
if (-not (Test-Path $exe)) { throw ("Build it first - not found: " + $exe) }

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Gui {
    [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr hProcess, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wp, IntPtr lp);
    public const uint GR_GDIOBJECTS = 0;
    public const uint GR_USEROBJECTS = 1;
    public const uint WM_DISPLAYCHANGE = 0x007E;
}
'@

Get-Process LumenDeck -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

# Poll for the window rather than sleeping a guessed startup interval. The form
# appears before its background DDC enumeration finishes, which is intentional.
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $exe -PassThru
$hwnd = 0
while ($sw.Elapsed.TotalSeconds -lt 30) {
    Start-Sleep -Milliseconds 200
    $p.Refresh()
    if ($p.HasExited) { throw ('App exited during startup, code ' + $p.ExitCode) }
    if ($p.MainWindowHandle -ne 0) { $hwnd = $p.MainWindowHandle; break }
}
$sw.Stop()
if ($hwnd -eq 0) { throw 'No main window appeared within 30 s.' }
Write-Host ('window appeared after ' + [math]::Round($sw.Elapsed.TotalSeconds, 2) + ' s')

function Sample {
    $p.Refresh()
    [pscustomobject]@{
        Gdi  = [Gui]::GetGuiResources($p.Handle, [Gui]::GR_GDIOBJECTS)
        User = [Gui]::GetGuiResources($p.Handle, [Gui]::GR_USEROBJECTS)
        Ram  = [math]::Round($p.WorkingSet64 / 1MB, 1)
    }
}

# One warm-up rebuild first: the very first one settles lazily-created objects,
# so counting from a cold start would report growth that is not a leak.
[void][Gui]::PostMessage($hwnd, [Gui]::WM_DISPLAYCHANGE, [IntPtr]::Zero, [IntPtr]::Zero)
Start-Sleep -Milliseconds $SettleMs

$before = Sample
Write-Host ''
Write-Host ('baseline        GDI=' + $before.Gdi + '  USER=' + $before.User + '  RAM=' + $before.Ram + ' MB')
Write-Host ''

for ($i = 1; $i -le $Cycles; $i++) {
    [void][Gui]::PostMessage($hwnd, [Gui]::WM_DISPLAYCHANGE, [IntPtr]::Zero, [IntPtr]::Zero)
    Start-Sleep -Milliseconds $SettleMs
    $s = Sample
    Write-Host ('rebuild ' + ([string]$i).PadLeft(2) + '      GDI=' + ([string]$s.Gdi).PadLeft(4) +
                '  USER=' + ([string]$s.User).PadLeft(4) + '  RAM=' + $s.Ram + ' MB')
}

$after = Sample
$dGdi = $after.Gdi - $before.Gdi
$dUser = $after.User - $before.User

Write-Host ''
Write-Host ('delta over ' + $Cycles + ' rebuilds:  GDI ' + $dGdi + '   USER ' + $dUser)

# A handful of objects of drift is normal - the GC has not necessarily run, and
# WinForms caches some resources. Growth proportional to the cycle count is the
# signature of a real leak.
$perCycleGdi = [math]::Round($dGdi / $Cycles, 2)
$perCycleUser = [math]::Round($dUser / $Cycles, 2)
Write-Host ('per rebuild:              GDI ' + $perCycleGdi + '   USER ' + $perCycleUser)
Write-Host ''

if ($perCycleGdi -ge 5 -or $perCycleUser -ge 5) {
    Write-Warning 'LEAK: object count grows with every rebuild.'
    $code = 1
} else {
    Write-Host 'PASS - no growth proportional to rebuild count.'
    $code = 0
}

Write-Host ''
Write-Host 'Leaving the app running so it can be looked at.'
exit $code

# Test-Gamma.ps1
# What colour temperature is actually reaching the glass.
#
# This test exists because the app spent its whole life shipping a warmth that
# was far stronger than the number on the slider, and nothing in the app could
# tell. Two faults, both invisible from inside:
#
# 1. WRONG DOMAIN. A gamma ramp holds encoded values and the display raises them
#    to about 2.2 before any light comes out. Scaling a ramp entry by f scales
#    the light by f^2.2. The app scaled the ramp by the LINEAR white point, so
#    the Night preset asked for 4600 K and delivered nearer 3000 K.
#
# 2. COMPOUNDING. SetDeviceGammaRamp outlives the process. The enumerator
#    captured whatever ramp was loaded and called it the display's baseline, so
#    every launch, every Refresh and every display change composed the saved
#    warmth onto the previous warmth. "Warmth off" restored the polluted
#    capture, so nothing undid it.
#
# Both are measurable from outside the app, which is what this does: read the
# ramp each display is really running, work back to the light it produces, and
# name the temperature that implies.
#
# Needs real displays, like the other two tests here, so it is not in CI.
#
# Usage:
#   powershell -File test\Test-Gamma.ps1
#   powershell -File test\Test-Gamma.ps1 -ExpectKelvin 4600
#   powershell -File test\Test-Gamma.ps1 -ExpectNeutral

[CmdletBinding()]
param(
    # Temperature the app was last told to apply, if you want this checked.
    [int]$ExpectKelvin = 0,

    # Require every display to be untinted instead.
    [switch]$ExpectNeutral,

    # How far the delivered temperature may sit from the requested one. The
    # white point table is sampled every 500 K and interpolated, so a couple of
    # hundred kelvin is measurement, not error.
    [int]$ToleranceKelvin = 250
)

$ErrorActionPreference = 'Stop'

Add-Type -Namespace LumenDeckTest -Name Gdi -MemberDefinition @'
[DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateDCW")]
public static extern IntPtr CreateDC(string driver, string device, string output, IntPtr initData);

[DllImport("gdi32.dll")]
public static extern bool DeleteDC(IntPtr hdc);

[DllImport("gdi32.dll")]
public static extern bool GetDeviceGammaRamp(IntPtr hdc, ushort[] ramp);
'@

# The same linear white points the app uses, normalised to 1.0 at 6500 K.
$whitePoints = @(
    @{ K = 6500; G = 1.0000; B = 1.0000 }
    @{ K = 6000; G = 0.9576; B = 0.9151 }
    @{ K = 5500; G = 0.9098; B = 0.8283 }
    @{ K = 5000; G = 0.8577; B = 0.7215 }
    @{ K = 4500; G = 0.8003; B = 0.6127 }
    @{ K = 4000; G = 0.7350; B = 0.4977 }
    @{ K = 3500; G = 0.6596; B = 0.3690 }
    @{ K = 3000; G = 0.5697; B = 0.2231 }
)

# Blue falls monotonically with temperature, so it inverts cleanly.
function Get-KelvinFromBlue([double]$linearBlue) {
    if ($linearBlue -ge $whitePoints[0].B) { return $whitePoints[0].K }
    for ($i = 0; $i -lt $whitePoints.Count - 1; $i++) {
        $hi = $whitePoints[$i]
        $lo = $whitePoints[$i + 1]
        if ($linearBlue -le $hi.B -and $linearBlue -ge $lo.B) {
            $span = $hi.B - $lo.B
            $t = if ($span -eq 0) { 0 } else { ($hi.B - $linearBlue) / $span }
            return [int][math]::Round($hi.K + ($lo.K - $hi.K) * $t)
        }
    }
    return $whitePoints[-1].K   # below the table: colder than 3000 K is off the end
}

function Read-Ramp([string]$device) {
    $hdc = [LumenDeckTest.Gdi]::CreateDC('DISPLAY', $device, $null, [IntPtr]::Zero)
    if ($hdc -eq [IntPtr]::Zero) { return $null }
    try {
        $ramp = New-Object uint16[] 768
        if (-not [LumenDeckTest.Gdi]::GetDeviceGammaRamp($hdc, $ramp)) { return $null }
        return $ramp
    } finally {
        [void][LumenDeckTest.Gdi]::DeleteDC($hdc)
    }
}

# A channel LumenDeck wrote is the identity scaled by one constant. Report both
# the scale and how far the channel strays from a straight line, because a real
# ICC or colorimeter LUT is a measured curve and strays a great deal.
function Measure-Channel([uint16[]]$ramp, [int]$channel) {
    $offset = $channel * 256
    $scale = $ramp[$offset + 255] / 65535.0
    $worst = 0.0
    for ($i = 0; $i -lt 256; $i++) {
        $drift = [math]::Abs($ramp[$offset + $i] - ($i * 257.0 * $scale))
        if ($drift -gt $worst) { $worst = $drift }
    }
    return [pscustomobject]@{ Scale = $scale; Drift = $worst }
}

$rows = @()
foreach ($n in 1..16) {
    $device = "\\.\DISPLAY$n"
    $ramp = Read-Ramp $device
    if ($null -eq $ramp) { continue }

    $r = Measure-Channel $ramp 0
    $g = Measure-Channel $ramp 1
    $b = Measure-Channel $ramp 2

    # Encoded scale to emitted light.
    $linearG = [math]::Pow($g.Scale, 2.2)
    $linearB = [math]::Pow($b.Scale, 2.2)
    $delivered = Get-KelvinFromBlue $linearB

    $rows += [pscustomobject]@{
        Display     = $device
        EncodedRGB  = '{0:0.000} {1:0.000} {2:0.000}' -f $r.Scale, $g.Scale, $b.Scale
        LinearGB    = '{0:0.000} {1:0.000}' -f $linearG, $linearB
        Delivered   = if ($linearB -ge 0.999 -and $linearG -ge 0.999) { 'neutral' } else { "$delivered K" }
        DeliveredK  = $delivered
        Neutral     = ($linearB -ge 0.999 -and $linearG -ge 0.999)
        LinearShape = ([math]::Max([math]::Max($r.Drift, $g.Drift), $b.Drift) -le 192)
        RedTouched  = ($r.Scale -lt 0.995)
    }
}

if ($rows.Count -eq 0) { throw 'No display would report a gamma ramp.' }

Write-Host ''
Write-Host 'Gamma ramp actually loaded on each display' -ForegroundColor Cyan
$rows | Format-Table Display, EncodedRGB, LinearGB, Delivered -AutoSize

$fail = @()

foreach ($row in $rows) {
    if ($row.RedTouched) {
        # Warming only ever removes green and blue. A ramp that dims red belongs
        # to something else and LumenDeck must not have written it.
        $fail += ('{0}: red channel is scaled to {1} - not a LumenDeck ramp' -f $row.Display, $row.EncodedRGB)
    }
    if (-not $row.LinearShape) {
        Write-Host ('{0}: ramp is not a scaled identity - a calibration LUT is loaded, so the temperature above is only the tint on top of it.' -f $row.Display) -ForegroundColor Yellow
    }
}

if ($ExpectNeutral) {
    foreach ($row in $rows) {
        if (-not $row.Neutral) {
            $fail += ('{0}: expected neutral, delivering {1}' -f $row.Display, $row.Delivered)
        }
    }
}
elseif ($ExpectKelvin -gt 0) {
    foreach ($row in $rows) {
        $off = [math]::Abs($row.DeliveredK - $ExpectKelvin)
        if ($off -gt $ToleranceKelvin) {
            $fail += ('{0}: asked for {1} K, delivering {2} K - {3} K out' -f $row.Display, $ExpectKelvin, $row.DeliveredK, $off)
        }
    }
}

Write-Host ''
if ($fail.Count -gt 0) {
    foreach ($f in $fail) { Write-Host ('FAIL  ' + $f) -ForegroundColor Red }
    exit 1
}

if ($ExpectNeutral) { Write-Host 'PASS  every display is untinted.' -ForegroundColor Green }
elseif ($ExpectKelvin -gt 0) { Write-Host ("PASS  every display is delivering $ExpectKelvin K within $ToleranceKelvin K.") -ForegroundColor Green }
else { Write-Host 'Reported only - pass -ExpectKelvin or -ExpectNeutral to assert.' -ForegroundColor Gray }
exit 0

# Material response bench

A device-free WPF scene using the **production** material window and a synthetic
owner backdrop. It references only the presentation assembly. It reads neither
product settings nor monitor identities, and its host accepts no commands.

```powershell
dotnet build experiments/MaterialBench/MaterialBench.csproj -c Release
$exe = Resolve-Path experiments/MaterialBench/bin/Release/net10.0-windows/LumenDeck.MaterialBench.exe
Start-Process $exe -ArgumentList '--capture','artifacts/new-active-run' -WindowStyle Hidden -Wait
```

This opens two temporary owned windows. No manual action is required. Active-only
mode waits up to ten seconds for ordinary activation, then records a blocked run
if native foreground is unavailable. No foreground-lock workaround, injected
input, policy alteration, UI Automation or physical display operation is used.
Use `--capture-visible` only for a separately labeled visible-scene run; it does
not relax active-only evidence into a success. Use a **new output directory**.

The bench switches A/B synthetic patterns for Smoke/Frost Glass, Solid and Mica,
plus Ceramic Solid. Capture uses GDI CopyFromScreen on the exact client rectangle
with a sampled native occlusion grid before/after. Small occluders between grid
points are a residual limitation. Native foreground is checked before/after;
WPF IsActive alone is insufficient. SDR pixels are not an HDR measurement.

Each saved frame updates a partial manifest with ROI geometry, runtime, Windows,
transparency, native request readback, assembly build/hash and foreground state.
Failures retain partial evidence and failure.json. A build suffix identifies the
Git base; the assembly hash also matters for an uncommitted working tree.

Inspect images and compare text-free regions between matched A/B frames. Solid is
the null control. Mica is wallpaper-informed, so it should not be judged by live
window-background response. A changed pixel does not establish blur quality,
readability, performance or accessibility. A DWM readback is configuration only.

Keep raw output private: Mica can incorporate the real wallpaper even though the
owner window is synthetic. Do not publish desktop content or Mica captures.
The bench is excluded from the production solution and portable package.

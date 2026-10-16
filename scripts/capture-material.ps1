param([string]$OutputDirectory = 'docs/assets')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$dirty = git status --porcelain -- src test Directory.Build.props scripts/capture-material.ps1
if ($dirty) { throw 'Commit implementation and capture tools before exporting source-bound assets.' }
$source = (git rev-parse HEAD).Trim()
$captures = Join-Path 'artifacts/material-capture' ([guid]::NewGuid().ToString('N'))
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --material-only $captures
if ($LASTEXITCODE -ne 0) { throw 'Material integration and render checks failed.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$images = @()
foreach ($finish in 'smoke', 'frost', 'ceramic') {
    foreach ($suffix in '', '-compact') {
        $name = 'material-' + $finish + $suffix + '.png'
        $destination = Join-Path $OutputDirectory $name
        Copy-Item -LiteralPath (Join-Path $captures ($finish + $suffix + '.png')) -Destination $destination -Force
        $images += [ordered]@{ file=$name; sha256=(Get-FileHash -LiteralPath $destination).Hash.ToLowerInvariant() }
    }
}
$menuName = 'material-theme-menu.png'
$menuDestination = Join-Path $OutputDirectory $menuName
Copy-Item -LiteralPath (Join-Path $captures 'ceramic-theme-menu.png') -Destination $menuDestination -Force
$images += [ordered]@{ file=$menuName; sha256=(Get-FileHash -LiteralPath $menuDestination).Hash.ToLowerInvariant() }
[ordered]@{
    sourceCommit=$source
    generator='scripts/capture-material.ps1'
    evidence='WPF client reference renders with synthetic displays; opaque fallback forced; DWM compositor and native frame are not captured.'
    regularWindowDip=@(880,830)
    compactWindowDip=@(660,610)
    renderDpi=144
    images=$images
} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'material-reference.json')

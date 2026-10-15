param([string]$OutputDirectory = "docs/assets")
$ErrorActionPreference = "Stop"
Set-Location (Split-Path -Parent $PSScriptRoot)
$dirty = git status --porcelain -- src test Directory.Build.props scripts/capture-showcase.ps1 scripts/compose-showcase.py
if ($dirty) { throw "Commit the implementation and capture tools before creating source-bound public assets." }
dotnet build LumenDeck.slnx -c Release --no-restore /warnaserror
if ($LASTEXITCODE -ne 0) { throw "Release build failed." }
$captures = Join-Path 'artifacts/showcase' ([guid]::NewGuid().ToString('N'))
dotnet run --project test/AppTests/AppTests.csproj -c Release -- --showcase-only $captures
if ($LASTEXITCODE -ne 0) { throw "Fixture capture checks failed." }
python scripts/compose-showcase.py $captures $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "Showcase export failed." }

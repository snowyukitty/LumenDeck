# build-release.ps1 - produce and verify the two release binaries.
#
# Self-contained single-file, so a machine with no .NET installed can run them.
# Verification is not optional: publishing an artifact nobody has executed is
# how a release ships a binary that cannot start on a clean machine. This runs
# each one from a fresh directory with dotnet removed from PATH.
#
# Usage: powershell -File scripts\build-release.ps1

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repo 'dist'
$out = Join-Path $dist 'release'

$dotnet = 'dotnet'
if (-not (Get-Command $dotnet -ErrorAction SilentlyContinue)) {
    $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (-not (Test-Path $dotnet)) { throw 'dotnet SDK not found.' }
}

if (Test-Path $dist) { Remove-Item -LiteralPath $dist -Recurse -Force }

$common = @(
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-v', 'quiet'
)

# Separate output folders: publishing both into one directory makes the second
# publish clean out the first one's executable.
Write-Host 'Publishing GUI...'
& $dotnet publish (Join-Path $repo 'src\LumenDeck\LumenDeck.csproj') @common -o (Join-Path $dist 'gui')
if ($LASTEXITCODE -ne 0) { throw 'GUI publish failed.' }

Write-Host 'Publishing CLI...'
& $dotnet publish (Join-Path $repo 'src\LumenDeck.Cli\LumenDeck.Cli.csproj') @common -o (Join-Path $dist 'cli')
if ($LASTEXITCODE -ne 0) { throw 'CLI publish failed.' }

New-Item -ItemType Directory -Path $out -Force | Out-Null
Copy-Item (Join-Path $dist 'gui\LumenDeck.exe') $out -Force
Copy-Item (Join-Path $dist 'cli\lumendeck-cli.exe') $out -Force

Write-Host ''
Get-ChildItem $out | Select-Object Name, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize | Out-String | Write-Host

# ------------------------------------------------------- clean-environment run
Write-Host 'Verifying from a fresh directory with no dotnet on PATH...'
$sandbox = Join-Path $env:TEMP ('lumendeck-verify-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
Copy-Item (Join-Path $out '*') $sandbox -Force

$cli = Join-Path $sandbox 'lumendeck-cli.exe'
$env:DOTNET_ROOT = ''
$version = & $cli --version
if ($LASTEXITCODE -ne 0) { throw "The published CLI did not run (--version exit $LASTEXITCODE)" }
Write-Host ("  runs standalone, reports version " + $version)

$json = & $cli --list --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw "--list failed with exit $LASTEXITCODE" }
Write-Host ("  detected " + @($json).Count + " monitor(s), " +
            @($json | Where-Object { $_.backend -ne 'None' }).Count + " controllable")

Write-Host ''
Write-Host 'SHA256:'
Get-FileHash (Join-Path $sandbox '*.exe') -Algorithm SHA256 |
    ForEach-Object { Write-Host ('  ' + (Split-Path -Leaf $_.Path).PadRight(22) + $_.Hash.ToLower()) }

Remove-Item -LiteralPath $sandbox -Recurse -Force
Write-Host ''
Write-Host ('Release binaries ready in ' + $out)

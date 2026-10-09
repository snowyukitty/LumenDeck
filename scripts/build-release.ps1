# Build and verify a portable package without replacing previous builds.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$work = Join-Path $repo ('dist\build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$common = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true', '-v', 'quiet')
foreach ($build in @(@('LumenDeck', 'gui'), @('LumenDeck.Cli', 'cli'))) {
    $project = Join-Path $repo ('src\' + $build[0] + '\' + $build[0] + '.csproj')
    & dotnet publish $project @common -o (Join-Path $work $build[1])
    if ($LASTEXITCODE -ne 0) { throw ($build[0] + ' publish failed; prior builds were preserved.') }
}
& (Join-Path $PSScriptRoot 'package-release.ps1') `
    -Gui (Join-Path $work 'gui\LumenDeck.exe') `
    -Cli (Join-Path $work 'cli\lumendeck-cli.exe') `
    -OutputDirectory (Join-Path $work 'verified')

param([string[]]$Files)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'scripts\SourcePathGuard.ps1')
if (-not $Files) {
    $Files = foreach ($project in @('LumenDeck', 'LumenDeck.Cli', 'LumenDeck.Core')) {
        $assembly = if ($project -eq 'LumenDeck.Cli') { 'lumendeck-cli' } else { $project }
        Join-Path $repo "src\$project\bin\Release\net10.0-windows\$assembly.dll"
    }
}
foreach ($file in $Files) {
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $file).Path)
    Assert-LumenDeckSourcePaths $bytes (Split-Path -Leaf $file) $repo
}
Write-Host ('PASS: ' + @($Files).Count + ' release assembly files contain mapped paths and no physical checkout prefix.')

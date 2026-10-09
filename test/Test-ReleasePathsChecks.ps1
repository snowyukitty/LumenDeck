$ErrorActionPreference = 'Stop'
$guard = Join-Path $PSScriptRoot 'Test-ReleasePaths.ps1'
& $guard
$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo 'src\LumenDeck.Core\bin\Release\net10.0-windows\LumenDeck.Core.dll'
$original = [IO.File]::ReadAllBytes($source)
$boundary = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$work = Join-Path $boundary ('LumenDeck-release-paths-' + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Path $work
    $fixture = Join-Path $work 'fixture.dll'
    $variants = @(
        [Text.Encoding]::UTF8.GetBytes($repo + '\synthetic'),
        [Text.Encoding]::UTF8.GetBytes($repo.Replace('\', '/') + '/synthetic'),
        [Text.Encoding]::Unicode.GetBytes($repo + '\synthetic'),
        ([byte[]]@(0) + [Text.Encoding]::Unicode.GetBytes($repo + '\synthetic')),
        [Text.Encoding]::UTF8.GetBytes(($repo + '\synthetic').Replace('\', '\\')),
        [Text.Encoding]::Unicode.GetBytes(($repo + '\synthetic').Replace('\', '\\')),
        ([byte[]]@(0) + [Text.Encoding]::Unicode.GetBytes(($repo + '\synthetic').Replace('\', '\\')))
    )
    foreach ($variant in $variants) {
        $stream = [IO.File]::Create($fixture)
        try { $stream.Write($original, 0, $original.Length); $stream.Write($variant, 0, $variant.Length) }
        finally { $stream.Dispose() }
        $rejected = $false
        try { & $guard -Files $fixture } catch {
            if ($_.Exception.Message -notlike 'Physical checkout path remains*') { throw }
            $rejected = $true
        }
        if (-not $rejected) { throw 'Injected physical checkout prefix was accepted.' }
    }
    Write-Host 'PASS: raw and JSON-escaped physical paths rejected across UTF-8 and both UTF-16 byte alignments.'
} finally {
    $absoluteWork = [IO.Path]::GetFullPath($work)
    if (-not $absoluteWork.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $absoluteWork) -notlike 'LumenDeck-release-paths-*') { throw 'Unsafe release-path fixture cleanup.' }
    if (Test-Path -LiteralPath $absoluteWork) { Remove-Item -LiteralPath $absoluteWork -Recurse -Force }
}

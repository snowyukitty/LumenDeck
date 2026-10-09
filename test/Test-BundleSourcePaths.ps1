$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$guard = Join-Path $repo 'scripts\Test-BundleSourcePaths.ps1'
Add-Type -AssemblyName System.IO.Compression
$boundary = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$work = Join-Path $boundary ('LumenDeck-bundle-paths-' + [guid]::NewGuid().ToString('N'))

function New-BundleFixture([byte[]]$Payload, [bool]$Compressed, [string]$Mutation = '') {
    # Independent minimal v6 fixture layout; neither entry is executable code.
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream, [Text.Encoding]::UTF8, $true)
    try {
        $writer.Write((New-Object byte[] 256))
        $stream.Position = 64
        $signature = [byte[]]@(0x8b,0x12,0x02,0xb9,0x6a,0x61,0x20,0x38,0x72,0x7b,0x93,0x02,0x14,0xd7,0xa0,0x32,0x13,0xf5,0xb9,0xe6,0xef,0xae,0x33,0x18,0xee,0x3b,0x2d,0xce,0x24,0xb3,0x6a,0xae)
        $writer.Write($signature)
        if ($Mutation -eq 'missing-marker') {
            $stream.Position = 64; $writer.Write((New-Object byte[] 32))
        }
        if ($Mutation -eq 'ambiguous-marker') {
            $stream.Position = 128; $writer.Write($signature)
        }
        $stream.Position = 256
        $entries = @()
        foreach ($name in @('LumenDeck.dll','LumenDeck.Core.dll')) {
            $offset = $stream.Position
            $packed = $Payload
            if ($Compressed) {
                $buffer = [IO.MemoryStream]::new()
                try {
                    $encoder = [IO.Compression.DeflateStream]::new($buffer, [IO.Compression.CompressionMode]::Compress, $true)
                    try { $encoder.Write($Payload, 0, $Payload.Length) } finally { $encoder.Dispose() }
                    $packed = $buffer.ToArray()
                } finally { $buffer.Dispose() }
            }
            $writer.Write([byte[]]$packed)
            $entries += [pscustomobject]@{Name=$name;Offset=$offset;Size=$Payload.Length;PackedSize=$(if ($Compressed) { $packed.Length } else { 0 })}
        }
        $header = $stream.Position
        $writer.Write([uint32]$(if ($Mutation -eq 'version') { 7 } else { 6 }))
        $writer.Write([uint32]0); $writer.Write([int]2); $writer.Write('canary')
        $writer.Write((New-Object byte[] 40))
        for ($index = 0; $index -lt $entries.Count; $index++) {
            $entry = $entries[$index]
            $offset = $entry.Offset; $size = $entry.Size; $name = $entry.Name
            if ($index -eq 0) {
                if ($Mutation -eq 'offset') { $offset = -1 }
                if ($Mutation -eq 'oversize') { $size = 64MB + 1 }
                if ($Mutation -eq 'overflow') { $size-- }
                if ($Mutation -eq 'short') { $size++ }
                if ($Mutation -eq 'name') { $name = 'x' * 4097 }
            }
            if ($index -eq 1 -and $Mutation -eq 'missing') { $name = 'Other.dll' }
            if ($index -eq 1 -and $Mutation -eq 'duplicate') { $name = 'LumenDeck.dll' }
            $writer.Write([long]$offset); $writer.Write([long]$size); $writer.Write([long]$entry.PackedSize)
            $writer.Write([byte]1); $writer.Write([string]$name)
        }
        $stream.Position = 56; $writer.Write([long]$header); $writer.Flush()
        return ,$stream.ToArray()
    } finally { $writer.Dispose(); $stream.Dispose() }
}

try {
    $null = New-Item -ItemType Directory -Path $work
    $fixture = Join-Path $work 'fixture.exe'
    $mapped = [Text.Encoding]::UTF8.GetBytes('/_/synthetic/source.cs')
    foreach ($compressed in @($false, $true)) {
        [IO.File]::WriteAllBytes($fixture, (New-BundleFixture $mapped $compressed))
        & $guard -Executable $fixture -ApplicationAssembly 'LumenDeck.dll'
    }
    $cases = @(
        @{Mutation='missing-marker';Expected='Missing or ambiguous bundle marker.'},
        @{Mutation='ambiguous-marker';Expected='Missing or ambiguous bundle marker.'},
        @{Mutation='version';Expected='Unsupported bundle manifest.'},
        @{Mutation='offset';Expected='Invalid bundle entry bounds.'},
        @{Mutation='oversize';Expected='Invalid bundle entry bounds.'},
        @{Mutation='overflow';Expected='Decoded bundle entry exceeds its declared size.'},
        @{Mutation='short';Expected='Decoded bundle entry size differs from its manifest.'},
        @{Mutation='name';Expected='Invalid bundle string length.'},
        @{Mutation='missing';Expected='Application and Core assemblies must each appear once.'},
        @{Mutation='duplicate';Expected='Oversized or duplicate bundle entries.'},
        @{Mutation='';Payload=[Text.Encoding]::UTF8.GetBytes('unmapped compiler output');Expected='Mapped source path was not observed in LumenDeck.dll.'},
        @{Mutation='';Payload=[Text.Encoding]::UTF8.GetBytes('/_/synthetic ' + $repo + '\source.cs');Expected='Physical checkout path remains in LumenDeck.dll.'},
        @{Mutation='';Payload=[Text.Encoding]::UTF8.GetBytes('/_/synthetic ' + $repo.Replace('\', '/') + '/source.cs');Expected='Physical checkout path remains in LumenDeck.dll.'},
        @{Mutation='';Payload=[Text.Encoding]::Unicode.GetBytes('/_/synthetic ' + $repo + '\source.cs');Expected='Physical checkout path remains in LumenDeck.dll.'},
        @{Mutation='';Payload=([byte[]]@(0) + [Text.Encoding]::Unicode.GetBytes('/_/synthetic ' + $repo + '\source.cs'));Expected='Physical checkout path remains in LumenDeck.dll.'},
        @{Mutation='';Payload=[Text.Encoding]::UTF8.GetBytes('/_/synthetic ' + ($repo + '\source.cs').Replace('\', '\\'));Expected='Physical checkout path remains in LumenDeck.dll.'},
        @{Mutation='';Payload=[Text.Encoding]::Unicode.GetBytes('/_/synthetic ' + ($repo + '\source.cs').Replace('\', '\\'));Expected='Physical checkout path remains in LumenDeck.dll.'},
        @{Mutation='';Payload=([byte[]]@(0) + [Text.Encoding]::Unicode.GetBytes('/_/synthetic ' + ($repo + '\source.cs').Replace('\', '\\')));Expected='Physical checkout path remains in LumenDeck.dll.'}
    )
    foreach ($case in $cases) {
        $payload = if ($case.ContainsKey('Payload')) { [byte[]]$case.Payload } else { $mapped }
        [IO.File]::WriteAllBytes($fixture, (New-BundleFixture $payload $true $case.Mutation))
        $rejected = $false
        try { & $guard -Executable $fixture -ApplicationAssembly 'LumenDeck.dll' } catch {
            if ($_.Exception.Message -cne $case.Expected) { throw }
            $rejected = $true
        }
        if (-not $rejected) { throw 'Invalid or private-path bundle fixture was accepted.' }
    }
    Write-Host ('PASS: ' + ($cases.Count + 2) + ' compressed/uncompressed, private-path, malformed and bounded-decompression bundle checks.')
} finally {
    $absoluteWork = [IO.Path]::GetFullPath($work)
    if (-not $absoluteWork.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $absoluteWork) -notlike 'LumenDeck-bundle-paths-*') { throw 'Unsafe bundle fixture cleanup.' }
    if (Test-Path -LiteralPath $absoluteWork) { Remove-Item -LiteralPath $absoluteWork -Recurse -Force }
}

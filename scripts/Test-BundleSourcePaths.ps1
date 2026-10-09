param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][ValidateSet('LumenDeck.dll', 'lumendeck-cli.dll')][string]$ApplicationAssembly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SourcePathGuard.ps1')
Add-Type -AssemblyName System.IO.Compression
# .NET v6 bundle layout and raw Deflate compression:
# https://github.com/dotnet/runtime/tree/v10.0.0/src/installer/managed/Microsoft.NET.HostModel/Bundle
# Read bytes only. Never load assemblies, execute the host or extract entries.
$file = Get-Item -LiteralPath $Executable
if ($file.Length -gt 512MB) { throw 'Bundle exceeds inspection size limit.' }
$blob = [IO.File]::ReadAllBytes($file.FullName)
$latin = [Text.Encoding]::GetEncoding(28591)
$signature = [byte[]]@(0x8b,0x12,0x02,0xb9,0x6a,0x61,0x20,0x38,0x72,0x7b,0x93,0x02,0x14,0xd7,0xa0,0x32,0x13,0xf5,0xb9,0xe6,0xef,0xae,0x33,0x18,0xee,0x3b,0x2d,0xce,0x24,0xb3,0x6a,0xae)
$text = $latin.GetString($blob)
$marker = $text.IndexOf($latin.GetString($signature), [StringComparison]::Ordinal)
if ($marker -lt 8 -or $text.IndexOf($latin.GetString($signature), $marker + 1, [StringComparison]::Ordinal) -ge 0) {
    throw 'Missing or ambiguous bundle marker.'
}
$text = $null
$header = [BitConverter]::ToInt64($blob, $marker - 8)
if ($header -le 0 -or $header -gt $blob.Length - 12) { throw 'Invalid bundle manifest offset.' }
$reader = [IO.BinaryReader]::new([IO.MemoryStream]::new($blob, $false))
function Read-BundleName {
    [long]$length = 0
    for ($shift = 0; $shift -le 28; $shift += 7) {
        $value = $reader.ReadByte()
        $length = $length -bor (([long]($value -band 127)) -shl $shift)
        if ($value -lt 128) {
            if ($length -gt 4096 -or $length -gt $reader.BaseStream.Length - $reader.BaseStream.Position) {
                throw 'Invalid bundle string length.'
            }
            return [Text.UTF8Encoding]::new($false, $true).GetString($reader.ReadBytes([int]$length))
        }
    }
    throw 'Invalid bundle string length.'
}
try {
    $reader.BaseStream.Position = $header
    $major = $reader.ReadUInt32(); $minor = $reader.ReadUInt32(); $count = $reader.ReadInt32()
    if ($major -ne 6 -or $minor -ne 0 -or $count -le 0 -or $count -gt 4096) { throw 'Unsupported bundle manifest.' }
    $null = Read-BundleName
    if ($reader.ReadBytes(40).Length -ne 40) { throw 'Truncated bundle header.' }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $owned = [Collections.Generic.List[object]]::new()
    [long]$total = 0
    for ($index = 0; $index -lt $count; $index++) {
        $offset = $reader.ReadInt64(); $size = $reader.ReadInt64(); $compressed = $reader.ReadInt64()
        $kind = $reader.ReadByte(); $name = Read-BundleName
        $length = if ($compressed -gt 0) { $compressed } else { $size }
        if ($size -lt 0 -or $size -gt 64MB -or $compressed -lt 0 -or $compressed -gt 64MB -or
            $offset -lt 0 -or $offset -gt $blob.Length -or $length -gt $blob.Length - $offset) {
            throw 'Invalid bundle entry bounds.'
        }
        $total += $size
        if ($total -gt 512MB -or -not $names.Add($name)) { throw 'Oversized or duplicate bundle entries.' }
        if ($name -ceq $ApplicationAssembly -or $name -ceq 'LumenDeck.Core.dll') {
            if ($kind -ne 1) { throw 'Expected a managed application assembly.' }
            $owned.Add([pscustomobject]@{Name=$name;Offset=$offset;Size=$size;Compressed=$compressed;Length=$length})
        }
    }
    if ($owned.Count -ne 2) { throw 'Application and Core assemblies must each appear once.' }
    foreach ($entry in $owned) {
        if ($entry.Compressed -gt 0) {
            $entryStream = [IO.MemoryStream]::new($blob, [int]$entry.Offset, [int]$entry.Length, $false)
            $output = [IO.MemoryStream]::new()
            $decoder = $null
            try {
                $decoder = [IO.Compression.DeflateStream]::new($entryStream, [IO.Compression.CompressionMode]::Decompress)
                $buffer = New-Object byte[] 81920
                while (($read = $decoder.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    if ($output.Length + $read -gt $entry.Size) { throw 'Decoded bundle entry exceeds its declared size.' }
                    $output.Write($buffer, 0, $read)
                }
                if ($output.Length -ne $entry.Size) { throw 'Decoded bundle entry size differs from its manifest.' }
                $data = $output.ToArray()
            } finally {
                if ($decoder) { $decoder.Dispose() }
                $entryStream.Dispose(); $output.Dispose()
            }
        } else {
            $reader.BaseStream.Position = $entry.Offset
            $data = $reader.ReadBytes([int]$entry.Size)
        }
        Assert-LumenDeckSourcePaths $data $entry.Name (Split-Path -Parent $PSScriptRoot)
    }
    Write-Host 'PASS: bundled app/Core assemblies have mapped paths and no physical checkout prefix.'
} finally { $reader.Dispose() }

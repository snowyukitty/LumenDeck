# Shared byte inspection for compiler outputs and decoded bundle assemblies.
function Assert-LumenDeckSourcePaths([byte[]]$Bytes, [string]$Name, [string]$SourceRoot) {
    $prefixes = @(($SourceRoot.TrimEnd('\', '/') + '\'), ($SourceRoot.Replace('\', '/').TrimEnd('/') + '/'))
    $prefixes += $prefixes[0].Replace('\', '\\')
    $views = @([Text.Encoding]::UTF8.GetString($Bytes), [Text.Encoding]::Unicode.GetString($Bytes))
    if ($Bytes.Length -gt 1) { $views += [Text.Encoding]::Unicode.GetString($Bytes, 1, $Bytes.Length - 1) }
    foreach ($prefix in $prefixes) {
        if (@($views | Where-Object { $_.IndexOf($prefix, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) {
            throw ('Physical checkout path remains in ' + $Name + '.')
        }
    }
    if (-not @($views | Where-Object { $_.Contains('/_/') }).Count) {
        throw ('Mapped source path was not observed in ' + $Name + '.')
    }
}

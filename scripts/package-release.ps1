param(
    [Parameter(Mandatory = $true)][string]$Gui,
    [Parameter(Mandatory = $true)][string]$Cli,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$props = [xml](Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props'))
$version = (@($props.Project.PropertyGroup.Version) | Where-Object { $_ } | Select-Object -First 1).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'A numeric three-part version is required.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory; existing packages are preserved.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$stage = Join-Path $OutputDirectory 'files'
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item -LiteralPath $Gui -Destination (Join-Path $stage 'LumenDeck.exe')
Copy-Item -LiteralPath $Cli -Destination (Join-Path $stage 'lumendeck-cli.exe')
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $stage 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repo 'docs\portable-quickstart.txt') -Destination (Join-Path $stage 'QUICKSTART.txt')
$payload = @('LumenDeck.exe', 'lumendeck-cli.exe', 'LICENSE.txt', 'QUICKSTART.txt')
$hashes = foreach ($name in $payload) {
    (Get-FileHash -LiteralPath (Join-Path $stage $name) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $name
}
$hashes | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding Ascii
$archive = Join-Path $OutputDirectory ("LumenDeck-$version-win-x64.zip")
# Only the declared payload enters the archive, never publish folders or repo contents.
$files = @($payload + 'SHA256SUMS.txt' | ForEach-Object { Join-Path $stage $_ })
Compress-Archive -LiteralPath $files -DestinationPath $archive
try {
    & (Join-Path $PSScriptRoot 'Test-Package.ps1') -Archive $archive -ExpectedVersion $version
    $archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    ($archiveHash + '  ' + (Split-Path -Leaf $archive)) |
        Set-Content -LiteralPath ($archive + '.sha256') -Encoding Ascii
    Write-Host ('Verified portable package: ' + $archive)
} catch {
    # Retain failed build evidence, but do not leave a failed ZIP that looks deliverable.
    Move-Item -LiteralPath $archive -Destination ($archive + '.failed')
    throw
}

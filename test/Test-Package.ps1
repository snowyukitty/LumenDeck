param([Parameter(Mandatory = $true)][string]$Archive)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$verify = Join-Path $repo 'scripts\Test-Package.ps1'
$tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$work = Join-Path $tempRoot ('LumenDeck-package-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$original = @{}
foreach ($name in @('PATH', 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_MULTILEVEL_LOOKUP', 'DOTNET_BUNDLE_EXTRACT_BASE_DIR')) {
    $original[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
function Assert-Restored {
    foreach ($name in $original.Keys) {
        if ([Environment]::GetEnvironmentVariable($name, 'Process') -cne $original[$name]) {
            throw "Package verification changed caller environment variable $name."
        }
    }
}
function Assert-Rejected([string]$zip, [string]$version) {
    $rejected = $false
    try { & $verify -Archive $zip -ExpectedVersion $version } catch { $rejected = $true }
    if (-not $rejected) { throw 'Invalid package was accepted.' }
    Assert-Restored
}
try {
    $props = [xml](Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props'))
    $version = (@($props.Project.PropertyGroup.Version) | Where-Object { $_ } | Select-Object -First 1).Trim()
    & $verify -Archive $Archive -ExpectedVersion $version
    Assert-Restored
    Write-Host 'PASS: valid package and caller environment restoration.'
    $files = Join-Path $work 'files'
    Expand-Archive -LiteralPath $Archive -DestinationPath $files
    Set-Content -LiteralPath (Join-Path $files 'unexpected.txt') -Value 'not allowed'
    $extra = Join-Path $work 'extra.zip'
    Compress-Archive -Path (Join-Path $files '*') -DestinationPath $extra
    Assert-Rejected $extra $version
    Write-Host 'PASS: extra archive content rejected before execution.'
    Remove-Item -LiteralPath (Join-Path $files 'unexpected.txt')
    Add-Content -LiteralPath (Join-Path $files 'QUICKSTART.txt') -Value 'tampered'
    $tampered = Join-Path $work 'tampered.zip'
    Compress-Archive -Path (Join-Path $files '*') -DestinationPath $tampered
    Assert-Rejected $tampered $version
    Write-Host 'PASS: changed payload rejected by checksum.'
    Assert-Rejected $Archive '99.99.99'
    Write-Host 'PASS: incorrect version rejected.'
    # Fail after verifier changes its process environment, without launching an app.
    function Start-Process { throw 'Injected launch failure.' }
    $rootBefore = $original['DOTNET_ROOT']
    try {
        foreach ($state in @('missing', 'empty', 'populated')) {
            if ($state -eq 'missing') { Remove-Item -LiteralPath Env:\DOTNET_ROOT -ErrorAction SilentlyContinue }
            elseif ($state -eq 'empty') { [Environment]::SetEnvironmentVariable('DOTNET_ROOT', '', 'Process') }
            else { [Environment]::SetEnvironmentVariable('DOTNET_ROOT', 'Synthetic package fixture', 'Process') }
            # Preserve the exact state supported by this PowerShell/.NET host.
            $original['DOTNET_ROOT'] = [Environment]::GetEnvironmentVariable('DOTNET_ROOT', 'Process')
            Assert-Rejected $Archive $version
            Write-Host "PASS: launch failure restores the $state environment fixture."
        }
    } finally {
        if ($null -eq $rootBefore) { Remove-Item -LiteralPath Env:\DOTNET_ROOT -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $rootBefore, 'Process') }
        $original['DOTNET_ROOT'] = $rootBefore
        Remove-Item Function:\Start-Process
    }
    # Mock the running app: installer must refuse before any user-state mutation.
    function Get-Process { [pscustomobject]@{ ProcessName = 'LumenDeck' } }
    foreach ($mode in @('install', 'uninstall')) {
        $refused = $false
        try {
            if ($mode -eq 'uninstall') { & (Join-Path $repo 'install.ps1') -Uninstall }
            else { & (Join-Path $repo 'install.ps1') -SkipBuild }
        } catch {
            if ($_.Exception.Message -notlike 'Exit LumenDeck from its tray menu*') { throw }
            $refused = $true
        }
        if (-not $refused) { throw 'Installer did not refuse a running app.' }
    }
    Remove-Item Function:\Get-Process
    Write-Host 'PASS: install and uninstall refuse an injected running app.'
    & (Join-Path $PSScriptRoot 'Test-InstallSource.ps1') -Archive $Archive
    & (Join-Path $PSScriptRoot 'Test-InstallState.ps1')
    & (Join-Path $PSScriptRoot 'Test-BundleSourcePaths.ps1')
} finally {
    if (-not ([IO.Path]::GetFullPath($work)).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $work) -notlike 'LumenDeck-package-tests-*') { throw 'Unsafe package test cleanup path.' }
    Remove-Item -LiteralPath $work -Recurse -Force
}

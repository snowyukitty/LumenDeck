$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repo 'install.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Installer has parse errors.' }
# Execute the actual helper and uninstall body with controlled collaborators.
# Never open HKCU Environment/Run or resolve the user's real shortcuts.
$helper = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Update-UserPathValue' }, $false)
. ([scriptblock]::Create($helper.Extent.Text))
$branch = $ast.Find({ param($node) $node -is [Management.Automation.Language.IfStatementAst] -and $node.Clauses[0].Item1.Extent.Text -eq '$Uninstall' }, $false)
$body = $branch.Clauses[0].Item2.Extent.Text
$uninstall = [scriptblock]::Create($body.Substring(1, $body.Length - 2))
$checks = 0
function Assert-State([bool]$ok, [string]$message) {
    if (-not $ok) { throw $message }; $script:checks++
}
function New-FakeKey($value, $kind) {
    $key = [pscustomobject]@{Value=$value;Kind=$kind;Writes=0;Options=$null}
    $key | Add-Member ScriptMethod GetValue { param($name,$fallback,$options) $this.Options=$options; return $this.Value }
    $key | Add-Member ScriptMethod GetValueKind { param($name) return $this.Kind }
    $key | Add-Member ScriptMethod SetValue { param($name,$value,$kind) $this.Value=$value; $this.Kind=$kind; $this.Writes++ }
    return $key
}
$dir = 'C:\Synthetic\LumenDeck'
foreach ($kind in @([Microsoft.Win32.RegistryValueKind]::String, [Microsoft.Win32.RegistryValueKind]::ExpandString)) {
    $raw = '%USERPROFILE%\bin;;C:\Other\;'
    $key = New-FakeKey $raw $kind
    Assert-State (Update-UserPathValue $key $dir) 'Missing entry was not added.'
    Assert-State ($key.Value -ceq ($raw + ';' + $dir) -and $key.Kind -eq $kind) 'Adding PATH changed unrelated raw text or kind.'
    Assert-State ($key.Options -eq [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) 'PATH read permits expansion.'
    Assert-State (-not (Update-UserPathValue $key $dir.ToLowerInvariant())) 'Duplicate entry caused a write.'
    Assert-State (Update-UserPathValue $key $dir -Remove) 'Owned entry was not removed.'
    Assert-State ($key.Value -ceq $raw -and $key.Kind -eq $kind) 'Removing PATH changed unrelated raw text or kind.'
    Assert-State (-not (Update-UserPathValue $key $dir -Remove) -and $key.Writes -eq 2) 'Absent removal caused a write.'
}
$key = New-FakeKey $null ([Microsoft.Win32.RegistryValueKind]::Unknown)
Assert-State (-not (Update-UserPathValue $key $dir -Remove) -and $key.Writes -eq 0) 'Absent PATH was written on removal.'
Assert-State (Update-UserPathValue $key $dir) 'Absent PATH was not created.'
Assert-State ($key.Value -ceq $dir -and $key.Kind -eq [Microsoft.Win32.RegistryValueKind]::ExpandString) 'New PATH has incorrect text or kind.'
$key = New-FakeKey '%USERPROFILE%\bin' ([Microsoft.Win32.RegistryValueKind]::MultiString)
$refused = $false
try { $null = Update-UserPathValue $key $dir } catch { $refused = $_.Exception.Message -ceq 'User PATH has an unsupported registry value kind.' }
Assert-State ($refused -and $key.Writes -eq 0) 'Unsupported registry kind was overwritten.'

$boundary = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$work = Join-Path $boundary ('LumenDeck-install-state-' + [guid]::NewGuid().ToString('N'))
$savedLocal = $env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA = Join-Path $work 'local'
    $target = Join-Path $env:LOCALAPPDATA 'Programs\LumenDeck'
    $shortcut = Join-Path $work 'start.lnk'; $desktopLink = Join-Path $work 'desktop.lnk'
    $null = New-Item -ItemType Directory -Path $target
    $state = @{Run=$true;Path=$true;Mutations=0}
    function Get-ItemProperty { param($Path,$ErrorAction) [pscustomobject]@{LumenDeck='synthetic'} }
    function Remove-ItemProperty { param($Path,$Name,[switch]$Force) $state.Run=$false; $state.Mutations++ }
    function Update-UserPath { param($dir,[switch]$Remove) $state.Path=$false; $state.Mutations++; return $true }
    foreach ($lockedName in @('LumenDeck.exe','lumendeck-cli.exe')) {
        foreach ($name in @('LumenDeck.exe','lumendeck-cli.exe','foreign.txt')) { [IO.File]::WriteAllText((Join-Path $target $name), 'synthetic') }
        [IO.File]::WriteAllText($shortcut, 'synthetic'); [IO.File]::WriteAllText($desktopLink, 'synthetic')
        $locked = [IO.File]::Open((Join-Path $target $lockedName), 'Open', 'Read', 'Read')
        $failed = $false
        try { & $uninstall } catch { $failed = $true } finally { $locked.Dispose() }
        Assert-State $failed 'Uninstall unexpectedly removed a locked executable.'
        Assert-State ((Test-Path -LiteralPath $shortcut) -and (Test-Path -LiteralPath $desktopLink) -and $state.Run -and $state.Path -and $state.Mutations -eq 0) 'File-removal failure changed launch entries.'
    }
    & $uninstall
    Assert-State ((Test-Path -LiteralPath (Join-Path $target 'foreign.txt')) -and -not (Test-Path -LiteralPath (Join-Path $target 'LumenDeck.exe')) -and -not (Test-Path -LiteralPath (Join-Path $target 'lumendeck-cli.exe'))) 'Uninstall did not preserve foreign files.'
    Assert-State (-not (Test-Path -LiteralPath $shortcut) -and -not (Test-Path -LiteralPath $desktopLink) -and -not $state.Run -and -not $state.Path) 'Successful uninstall left controlled launch entries.'
} finally {
    $env:LOCALAPPDATA = $savedLocal
    $absoluteWork = [IO.Path]::GetFullPath($work)
    if (-not $absoluteWork.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $absoluteWork) -notlike 'LumenDeck-install-state-*') { throw 'Unsafe installer-state cleanup.' }
    if (Test-Path -LiteralPath $absoluteWork) { Remove-Item -LiteralPath $absoluteWork -Recurse -Force }
}
Write-Host ('PASS: ' + $checks + ' installer PATH/locked-file/state-preservation checks; no real registry or shortcuts changed.')

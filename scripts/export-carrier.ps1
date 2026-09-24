param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Explicit local delivery for consumers using the published mod layout as their HintPath.
# Ordinary builds, harnesses and packers never call this. This is neither a build nor publication.
$root = [IO.Path]::GetFullPath($ProjectRoot)
$source = Join-Path $root 'dist/build/Release/FerriteLib.UiKit.dll'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Build Release before exporting the carrier.' }
$env:FERRITELIB_EXPORT_SOURCE = $source
try {
    $configuration = & pwsh -NoProfile -NonInteractive -Command '$a=[Reflection.Assembly]::LoadFile($env:FERRITELIB_EXPORT_SOURCE); foreach($t in [Reflection.CustomAttributeData]::GetCustomAttributes($a)) { if($t.AttributeType.Name -eq "AssemblyConfigurationAttribute") { $t.ConstructorArguments[0].Value; break } }'
    if ($LASTEXITCODE -ne 0 -or $configuration -ne 'Release') { throw 'The export source must be a Release assembly.' }
} finally { Remove-Item Env:FERRITELIB_EXPORT_SOURCE -ErrorAction SilentlyContinue }
$directory = Join-Path $root '1.6/Assemblies'
$null = New-Item -ItemType Directory -Path $directory -Force
$target = Join-Path $directory 'FerriteLib.UiKit.dll'
$candidate = Join-Path $directory ('.carrier-' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    Copy-Item -LiteralPath $source -Destination $candidate
    $hash = (Get-FileHash -LiteralPath $source).Hash
    if ((Get-FileHash -LiteralPath $candidate).Hash -ne $hash) { throw 'Carrier copy differs from build input.' }
    if (Test-Path -LiteralPath $target) {
        [IO.File]::Replace($candidate, $target, [NullString]::Value)
    } else {
        [IO.File]::Move($candidate, $target)
    }
    $pdb = Join-Path $directory 'FerriteLib.UiKit.pdb'
    if (Test-Path -LiteralPath $pdb) { Remove-Item -LiteralPath $pdb -Force }
    $identity = [Diagnostics.FileVersionInfo]::GetVersionInfo($target).ProductVersion
    Write-Host "[carrier] identity=$identity sha256=$hash mtime=$((Get-Item -LiteralPath $target).LastWriteTimeUtc.ToString('o'))"
} finally {
    if (Test-Path -LiteralPath $candidate) { Remove-Item -LiteralPath $candidate -Force }
}

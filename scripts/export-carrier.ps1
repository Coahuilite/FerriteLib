param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [ValidateSet('RimWorld16', 'RimWorld17')][string]$Target = 'RimWorld16',
    [string]$BuildRoot = 'dist/build'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Explicit local delivery for consumers using the published mod layout as their HintPath.
# Ordinary builds, harnesses and packers never call this. This is neither a build nor publication.
$root = [IO.Path]::GetFullPath($ProjectRoot)
$buildDirectory = [IO.Path]::GetFullPath($BuildRoot, $root)
$buildPath = if ($Target -eq 'RimWorld16') { 'Release/FerriteLib.UiKit.dll' } else { 'Release/RimWorld17/FerriteLib.UiKit.dll' }
$source = Join-Path $buildDirectory $buildPath
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Build Release before exporting the carrier.' }
Import-Module (Join-Path $PSScriptRoot 'Release.Identity.psm1')
$metadata = Get-FerritePayloadMetadata -Path $source
if ($metadata.configuration -ne 'Release') { throw 'The export source must be a Release assembly.' }
Assert-FerritePayloadTarget -Metadata $metadata -Target $Target
$game = if ($Target -eq 'RimWorld16') { '1.6' } else { '1.7' }
$directory = Join-Path $root "$game/Assemblies"
$null = New-Item -ItemType Directory -Path $directory -Force
$destination = Join-Path $directory 'FerriteLib.UiKit.dll'
$candidate = Join-Path $directory ('.carrier-' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    Copy-Item -LiteralPath $source -Destination $candidate
    $hash = (Get-FileHash -LiteralPath $source).Hash
    if ((Get-FileHash -LiteralPath $candidate).Hash -ne $hash) { throw 'Carrier copy differs from build input.' }
    if (Test-Path -LiteralPath $destination) {
        [IO.File]::Replace($candidate, $destination, [NullString]::Value)
    } else {
        [IO.File]::Move($candidate, $destination)
    }
    $pdb = Join-Path $directory 'FerriteLib.UiKit.pdb'
    if (Test-Path -LiteralPath $pdb) { Remove-Item -LiteralPath $pdb -Force }
    $identity = [Diagnostics.FileVersionInfo]::GetVersionInfo($destination).ProductVersion
    Write-Host "[carrier] identity=$identity sha256=$hash mtime=$((Get-Item -LiteralPath $destination).LastWriteTimeUtc.ToString('o'))"
} finally {
    if (Test-Path -LiteralPath $candidate) { Remove-Item -LiteralPath $candidate -Force }
}

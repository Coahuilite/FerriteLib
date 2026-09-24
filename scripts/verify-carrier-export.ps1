param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot)
$dist = [IO.Path]::GetFullPath((Join-Path $root 'dist'))
$fixture = Join-Path $dist ('.verify-export-' + [guid]::NewGuid().ToString('N'))
$build = Join-Path $fixture 'dist/build/Release'
$null = New-Item -ItemType Directory -Path $build -Force
try {
    $release = Join-Path $root 'dist/build/Release/FerriteLib.UiKit.dll'
    $source = Join-Path $build 'FerriteLib.UiKit.dll'
    Copy-Item -LiteralPath $release -Destination $source
    $target = Join-Path $fixture '1.6/Assemblies/FerriteLib.UiKit.dll'
    $pdb = Join-Path $fixture '1.6/Assemblies/FerriteLib.UiKit.pdb'
    # Exercise both creation and replacement; the real compatibility carrier is never touched.
    & (Join-Path $PSScriptRoot 'export-carrier.ps1') -ProjectRoot $fixture
    [IO.File]::WriteAllText($pdb, 'stale debug symbols')
    & (Join-Path $PSScriptRoot 'export-carrier.ps1') -ProjectRoot $fixture
    if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $source).Hash) { throw 'Exported bytes differ.' }
    if (Test-Path -LiteralPath $pdb) { throw 'Export left stale compatibility symbols.' }
    $before = (Get-FileHash -LiteralPath $target).Hash + ':' + (Get-Item -LiteralPath $target).LastWriteTimeUtc.Ticks
    Copy-Item -LiteralPath (Join-Path $root 'dist/build/Dev/FerriteLib.UiKit.dll') -Destination $source -Force
    $refused = $false
    try { & (Join-Path $PSScriptRoot 'export-carrier.ps1') -ProjectRoot $fixture }
    catch {
        if ($_.Exception.Message -ne 'The export source must be a Release assembly.') { throw }
        $refused = $true
    }
    if (-not $refused) { throw 'Dev bytes were exported into the Release compatibility path.' }
    $after = (Get-FileHash -LiteralPath $target).Hash + ':' + (Get-Item -LiteralPath $target).LastWriteTimeUtc.Ticks
    if ($before -ne $after) { throw 'Rejected export changed the delivery.' }
    Write-Host '[carrier-export] creation/replacement passed; Dev refused without touching the delivery.'
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    if (-not $resolvedFixture.StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Fixture cleanup escaped dist.'
    }
    if (Test-Path -LiteralPath $resolvedFixture) { Remove-Item -LiteralPath $resolvedFixture -Recurse -Force }
}

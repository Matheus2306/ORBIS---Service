param(
    [ValidateSet('Small', 'Medium', 'Large')][string]$Level = 'Small',
    [ValidateSet('Uniform', 'HotTenant')][string]$Profile = 'Uniform',
    [ValidateRange(0, 2147483647)][int]$Seed = 42,
    [string]$PgBin = 'C:/Program Files/PostgreSQL/18/bin',
    [ValidateRange(1024, 65535)][int]$Port = 55439
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'with-local-postgres.ps1') -Purpose Performance -PgBin $PgBin -Port $Port -Action {
    param($runRoot)
    $manifest = Join-Path $runRoot 'dataset-manifest.json'
    dotnet run --project tools/Orbis.DataGenerator -c Release --no-build --no-restore -- $Level $Profile $Seed $manifest
    if ($LASTEXITCODE -ne 0) { throw 'Dataset generation failed; no valid evidence is available.' }
    Write-Output "Verified manifest: $manifest"
}
exit $LASTEXITCODE

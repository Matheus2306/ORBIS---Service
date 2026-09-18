param(
    [ValidateSet('Uniform', 'HotTenant')][string]$Profile = 'Uniform',
    [string]$PgBin = 'C:/Program Files/PostgreSQL/18/bin',
    [ValidateRange(1024, 65535)][int]$Port = 55439
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'with-local-postgres.ps1') -Purpose Performance -PgBin $PgBin -Port $Port -Action {
    param($runRoot, $databaseName)
    dotnet run --project tools/Orbis.DataGenerator -c Release --no-build --no-restore -- Small $Profile 42 (Join-Path $runRoot 'dataset-manifest.json')
    if ($LASTEXITCODE -ne 0) { throw 'Dataset preparation failed.' }
    & (Join-Path $PgBin 'psql') --no-psqlrc --host=127.0.0.1 --port=$Port --username=postgres --dbname=$databaseName --set=ON_ERROR_STOP=1 --quiet --file=infrastructure/postgresql/runtime-grants.sql
    if ($LASTEXITCODE -ne 0) { throw 'Runtime grants failed.' }
    dotnet run --project tools/Orbis.QueryProbe -c Release --no-build --no-restore -- $Profile (Join-Path $runRoot 'query-plans.json')
    if ($LASTEXITCODE -ne 0) { throw 'SQL plan capture failed.' }
    Write-Output "Query plan evidence: $runRoot"
}
exit $LASTEXITCODE

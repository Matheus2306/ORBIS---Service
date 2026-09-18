param(
    [string]$PgBin = 'C:/Program Files/PostgreSQL/18/bin',
    [ValidateRange(1024, 65535)][int]$Port = 55439
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'with-local-postgres.ps1') -PgBin $PgBin -Port $Port -Action {
    param($runRoot)
    dotnet test Orbis.Service.slnx -c Release --no-build --no-restore --logger trx --results-directory (Join-Path $runRoot 'test-results')
    if ($LASTEXITCODE -ne 0) { throw 'Release test suite failed.' }
}
exit $LASTEXITCODE

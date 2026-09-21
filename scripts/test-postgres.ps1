param(
    [string]$PgBin = 'C:/Program Files/PostgreSQL/18/bin',
    [ValidateRange(1024, 65535)][int]$Port = 55439,
    [string]$ResultsDirectory
)
$ErrorActionPreference = 'Stop'
$action = {
    param($runRoot)
    # O gate escolhe um diretório novo para não confundir evidência desta execução com TRX anteriores.
    $results = if ($ResultsDirectory) { $ResultsDirectory } else { Join-Path $runRoot 'test-results' }
    dotnet test Orbis.Service.slnx -c Release --no-build --no-restore --logger trx --results-directory $results
    if ($LASTEXITCODE -ne 0) { throw 'Release test suite failed.' }
}.GetNewClosure()
& (Join-Path $PSScriptRoot 'with-local-postgres.ps1') -PgBin $PgBin -Port $Port -Action $action
exit $LASTEXITCODE

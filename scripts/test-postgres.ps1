param(
    [string]$PgBin = 'C:/Program Files/PostgreSQL/18/bin',
    [ValidateRange(1024, 65535)][int]$Port = 55439,
    [string]$ResultsDirectory
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'load-generator.ps1')
$generator = Get-VerifiedLoadGenerator
$action = {
    param($runRoot)
    # O gate escolhe um diretório novo para não confundir evidência desta execução com TRX anteriores.
    $results = if ($ResultsDirectory) { $ResultsDirectory } else { Join-Path $runRoot 'test-results' }
    $previousGenerator = $env:ORBIS_VEGETA_PATH
    try {
        $env:ORBIS_VEGETA_PATH = $generator.binary
        dotnet test Orbis.Service.slnx -c Release --no-build --no-restore --logger trx --results-directory $results
        if ($LASTEXITCODE -ne 0) { throw 'Release test suite failed.' }
    } finally { $env:ORBIS_VEGETA_PATH = $previousGenerator }
}.GetNewClosure()
& (Join-Path $PSScriptRoot 'with-local-postgres.ps1') -PgBin $PgBin -Port $Port -Action $action
exit $LASTEXITCODE

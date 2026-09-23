param(
    [Parameter(Mandatory)][scriptblock]$Action,
    [ValidateSet('Testing', 'Performance')][string]$Purpose = 'Testing',
    [string]$PgBin = 'C:/Program Files/PostgreSQL/18/bin',
    [ValidateRange(1024, 65535)][int]$Port = 55439
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runRoot = Join-Path $projectRoot ('.artifacts/postgres/' + [Guid]::NewGuid().ToString('N'))
$dataPath = Join-Path $runRoot 'data'
$logPath = Join-Path $runRoot 'postgres.log'
$passwordPath = Join-Path $runRoot 'init-password.txt'
$pgCtl = Join-Path $PgBin 'pg_ctl'
$initDb = Join-Path $PgBin 'initdb'
$psql = Join-Path $PgBin 'psql'
$databaseName = $(if ($Purpose -eq 'Performance') { 'orbis_perf_' } else { 'orbis_test_' }) + [Guid]::NewGuid().ToString('N')
$adminPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$runtimePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$membershipPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$previousPassword = $env:PGPASSWORD
$previousSslMode = $env:PGSSLMODE
$previousSslRoot = $env:PGSSLROOTCERT
$previousAdmin = $env:ORBIS_TEST_ADMIN_CONNECTION
$previousRuntime = $env:ORBIS_TEST_RUNTIME_CONNECTION
$previousMembership = $env:ORBIS_TEST_MEMBERSHIP_CONNECTION
$previousDataset = $env:ORBIS_DATASET_CONNECTION
$started = $false
$testExitCode = 1

New-Item -ItemType Directory -Path $runRoot | Out-Null
try {
    # Segredos efêmeros não aparecem em argumentos, logs ou arquivos versionados.
    Set-Content -LiteralPath $passwordPath -Value $adminPassword -NoNewline
    & $initDb -D $dataPath --username=postgres --auth=scram-sha-256 --pwfile=$passwordPath --encoding=UTF8 --locale=C
    if ($LASTEXITCODE -ne 0) { throw 'Failed to initialize the isolated PostgreSQL cluster.' }
    Remove-Item -LiteralPath $passwordPath
    $tls = & (Join-Path $PSScriptRoot 'new-local-test-certificate.ps1') -Directory (Join-Path $runRoot 'tls')
    $certificatePath = $tls.Certificate.Replace('\', '/').Replace("'", "''")
    $keyPath = $tls.Key.Replace('\', '/').Replace("'", "''")
    Add-Content -LiteralPath (Join-Path $dataPath 'postgresql.conf') -Value @"
ssl = on
ssl_min_protocol_version = 'TLSv1.2'
ssl_cert_file = '$certificatePath'
ssl_key_file = '$keyPath'
"@
    # ssl=on sozinho ainda permite plaintext; a regra de acesso precisa recusá-lo explicitamente.
    Set-Content -LiteralPath (Join-Path $dataPath 'pg_hba.conf') -Value @"
hostssl all all 127.0.0.1/32 scram-sha-256
hostnossl all all 127.0.0.1/32 reject
"@
    $serverOptions = "-h 127.0.0.1 -p $Port -c max_connections=40 -c shared_buffers=64MB"
    if ($IsWindows) {
        # Start-Process -Wait aguarda a árvore inteira, incluindo o servidor que deve continuar vivo.
        $start = Start-Process -FilePath ($pgCtl + '.exe') -ArgumentList @('-D', "`"$dataPath`"", '-l', "`"$logPath`"", '-o', "`"$serverOptions`"", '-w', '-t', '20', 'start') -WindowStyle Hidden -PassThru
        $started = $true
        if (-not $start.WaitForExit(30000)) { throw 'PostgreSQL initialization exceeded its time budget.' }
        if ($start.ExitCode -ne 0) { throw "PostgreSQL did not start. Inspect $logPath" }
    } else {
        & $pgCtl -D $dataPath -l $logPath -o $serverOptions -w start
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL did not start.' }
    }
    $started = $true
    $env:PGPASSWORD = $adminPassword
    $env:PGSSLMODE = 'verify-full'
    $env:PGSSLROOTCERT = $tls.Root
    # O cluster acaba de ser criado; nenhuma base pré-existente é alvo deste script.
    "CREATE ROLE orbis_runtime LOGIN PASSWORD '$runtimePassword' NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE; CREATE ROLE orbis_membership_admin LOGIN PASSWORD '$membershipPassword' NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE; CREATE DATABASE $databaseName;" |
        & $psql --no-psqlrc --host=127.0.0.1 --port=$Port --username=postgres --dbname=postgres --set=ON_ERROR_STOP=1 --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Test database provisioning failed.' }
    $rootOption = $tls.Root.Replace('"', '""')
    $transport = "SSL Mode=VerifyFull;Root Certificate=`"$rootOption`""
    $env:ORBIS_TEST_ADMIN_CONNECTION = "Host=127.0.0.1;Port=$Port;Database=$databaseName;Username=postgres;Password=$adminPassword;Maximum Pool Size=5;Timeout=5;Command Timeout=10;$transport"
    $env:ORBIS_TEST_RUNTIME_CONNECTION = "Host=127.0.0.1;Port=$Port;Database=$databaseName;Username=orbis_runtime;Password=$runtimePassword;Maximum Pool Size=12;Timeout=5;Command Timeout=10;$transport"
    $env:ORBIS_TEST_MEMBERSHIP_CONNECTION = "Host=127.0.0.1;Port=$Port;Database=$databaseName;Username=orbis_membership_admin;Password=$membershipPassword;Maximum Pool Size=8;Timeout=10;Command Timeout=10;$transport"
    $env:ORBIS_DATASET_CONNECTION = $env:ORBIS_TEST_ADMIN_CONNECTION
    Push-Location $projectRoot
    try {
        # O chamador fornece apenas uma ação local; provisionamento e encerramento têm uma única implementação.
        & $Action $runRoot $databaseName
        $testExitCode = 0
    } finally { Pop-Location }
} finally {
    $env:PGPASSWORD = $previousPassword
    $env:PGSSLMODE = $previousSslMode
    $env:PGSSLROOTCERT = $previousSslRoot
    $env:ORBIS_TEST_ADMIN_CONNECTION = $previousAdmin
    $env:ORBIS_TEST_RUNTIME_CONNECTION = $previousRuntime
    $env:ORBIS_TEST_MEMBERSHIP_CONNECTION = $previousMembership
    $env:ORBIS_DATASET_CONNECTION = $previousDataset
    if (Test-Path -LiteralPath $passwordPath) { Remove-Item -LiteralPath $passwordPath }
    if ($started) {
        & $pgCtl -D $dataPath -m fast -w stop
        if ($LASTEXITCODE -ne 0) { Write-Warning "Cluster cleanup failed; inspect $dataPath"; $testExitCode = 1 }
    }
    Write-Output "Local test evidence directory (review before sharing): $runRoot"
}
exit $testExitCode

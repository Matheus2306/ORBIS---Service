param(
    [string]$PgBin = 'C:/Program Files/PostgreSQL/18/bin',
    [ValidateRange(1024, 65535)][int]$Port = 55439,
    [string]$GitleaksPath
)
$ErrorActionPreference = 'Stop'
# Os códigos nativos são inspecionados explicitamente e preservados no manifesto.
$PSNativeCommandUseErrorActionPreference = $false
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'validation-rules.ps1')
if (-not $GitleaksPath) { $GitleaksPath = Join-Path $projectRoot '.artifacts/tools/gitleaks-8.30.1/gitleaks.exe' }
$runRoot = Join-Path $projectRoot ('.artifacts/validation/' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($runRoot)
$checks = [Collections.Generic.List[object]]::new()
$manifest = [ordered]@{ schemaVersion=1; startedUtc=[DateTimeOffset]::UtcNow.ToString('O'); status='failed'; scope='local-engineering'; productionApproved=$false; checks=$checks;
    pending=@('remote-ci', 'container-build-scan', 'authenticated-dast', 'performance-baseline', 'backup-restore', 'deployment-rollback') }

function Invoke-Check([string]$Name, [string]$Executable, [string[]]$Arguments) {
    $log = Join-Path $runRoot "$Name.log"
    $watch = [Diagnostics.Stopwatch]::StartNew()
    & $Executable @Arguments > $log 2>&1
    $code = $LASTEXITCODE
    $checks.Add([ordered]@{ name=$Name; exitCode=$code; seconds=[math]::Round($watch.Elapsed.TotalSeconds,3); artifact=[IO.Path]::GetFileName($log);
        sha256=(Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash })
    if ($code -ne 0) { throw "Check '$Name' failed. Inspect its local artifact." }
    Write-Host "$Name passed."
    return $log
}

function Get-SourceSnapshot {
    $paths = @(& $git -c "safe.directory=$projectRoot" ls-files --cached --others --exclude-standard | Sort-Object -Unique)
    if ($LASTEXITCODE -ne 0 -or $paths.Count -eq 0) { throw 'Cannot identify the evaluated source tree.' }
    $files = @($paths | ForEach-Object {
        $path = Join-Path $projectRoot $_
        [ordered]@{ path=$_; sha256=$(if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'deleted' }) }
    })
    $bytes = [Text.Encoding]::UTF8.GetBytes(($files | ConvertTo-Json -Depth 5 -Compress))
    return @{ sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)); files=$files }
}

Push-Location $projectRoot
try {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
    $git = (Get-Command git -ErrorAction Stop).Source
    if (-not (Test-Path -LiteralPath $GitleaksPath -PathType Leaf)) { throw 'Pinned Gitleaks is missing; no scan was skipped.' }
    $scannerVersion = @(& $GitleaksPath version)
    if ($LASTEXITCODE -ne 0 -or ($scannerVersion -join '').Trim() -ne '8.30.1') { throw 'Gitleaks version must be 8.30.1.' }
    $manifest.scannerSha256 = (Get-FileHash -LiteralPath $GitleaksPath -Algorithm SHA256).Hash
    $manifest.commit = (& $git -c "safe.directory=$projectRoot" rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve Git HEAD.' }
    $manifest.source = Get-SourceSnapshot
    $manifest.dirty = @(& $git -c "safe.directory=$projectRoot" status --porcelain).Count -gt 0
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read Git status.' }
    [xml]$solution = Get-Content -LiteralPath 'Orbis.Service.slnx' -Raw
    $projects = @($solution.SelectNodes('//Project') | ForEach-Object { Join-Path $projectRoot $_.Path })
    $testAssemblies = @($projects | Where-Object {
        [xml]$project = Get-Content -LiteralPath $_ -Raw
        $project.SelectNodes('//IsTestProject[text()="true"]').Count -gt 0
    } | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) + '.dll' })
    if ($testAssemblies.Count -eq 0) { throw 'No test projects found.' }
    Invoke-Check 'gate-self-tests' $pwsh @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'test-validation-rules.ps1')) | Out-Null
    Invoke-Check 'restore' $dotnet @('restore', '--locked-mode', '--force', '--no-http-cache') | Out-Null
    Invoke-Check 'tool-restore' $dotnet @('tool', 'restore') | Out-Null
    Invoke-Check 'format' $dotnet @('format', '--verify-no-changes', '--no-restore') | Out-Null
    Invoke-Check 'build' $dotnet @('build', '-c', 'Release', '--no-restore', '-warnaserror') | Out-Null
    $results = Join-Path $runRoot 'test-results'
    Invoke-Check 'tests' $pwsh @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'test-postgres.ps1'), '-PgBin', $PgBin, '-Port', "$Port", '-ResultsDirectory', $results) | Out-Null
    $trxFiles = @(Get-ChildItem -LiteralPath $results -Filter '*.trx')
    $trx = @($trxFiles | ForEach-Object { [xml](Get-Content -LiteralPath $_.FullName -Raw) })
    $manifest.testsPassed = Assert-TestResults $trx $testAssemblies
    $manifest.testArtifacts = @($trxFiles | ForEach-Object { @{file=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
    # JSON é interpretado: o comando de listagem pode retornar zero mesmo quando encontra vulnerabilidades.
    $auditPath = Invoke-Check 'nuget-audit' $dotnet @('list', 'package', '--vulnerable', '--include-transitive', '--no-restore', '--format', 'json', '--output-version', '1')
    Assert-NuGetAudit (Get-Content -LiteralPath $auditPath -Raw | ConvertFrom-Json -AsHashtable) $projects
    Invoke-Check 'secrets' $GitleaksPath @('dir', '.', '--redact', '--no-banner') | Out-Null
    foreach ($context in @('DirectoryDbContext', 'TenantDbContext')) {
        Invoke-Check "model-$context" $dotnet @('ef', 'migrations', 'has-pending-model-changes', '--context', $context, '--project', 'src/Orbis.Infrastructure', '--configuration', 'Release', '--no-build') | Out-Null
    }
    Invoke-Check 'diff' $git @('-c', "safe.directory=$projectRoot", 'diff', '--check', 'HEAD') | Out-Null
    if ((Get-SourceSnapshot).sha256 -ne $manifest.source.sha256) { throw 'Source tree changed while the gate was running; rerun on a stable snapshot.' }
    $manifest.status = 'passed'
} catch {
    $manifest.failure = $_.Exception.Message
    Write-Error -Message 'Local gate failed; inspect its manifest and logs. No production approval was issued.' -ErrorAction Continue
} finally {
    $manifest.finishedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $manifestPath = Join-Path $runRoot 'manifest.json'
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Pop-Location
    Write-Output "Validation evidence: $manifestPath"
}
if ($manifest.status -ne 'passed') { exit 1 }
exit 0

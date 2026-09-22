param()
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') {
    throw 'This pinned bootstrap has only been validated for Windows x64.'
}
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pinsRoot = Join-Path $projectRoot 'performance-tests/vegeta'
$pins = Get-Content -LiteralPath (Join-Path $pinsRoot 'toolchain.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'load-generator.ps1')
$toolsRoot = Join-Path $projectRoot '.artifacts/load-generator'
$runRoot = Join-Path $toolsRoot ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($runRoot)
$manifest = [ordered]@{ schemaVersion=1; status='failed'; startedUtc=[DateTimeOffset]::UtcNow.ToString('O'); version=$pins.version }
$manifest.inputs = @('toolchain.json', 'go.mod', 'go.sum') | ForEach-Object { @{ file=$_; sha256=(Get-FileHash (Join-Path $pinsRoot $_)).Hash } }
$environment = @{
    GOENV='off'; GOTOOLCHAIN='local'; GOTELEMETRY='off'; CGO_ENABLED='0'; GOOS='windows'; GOARCH='amd64'; GOAMD64='v1';
    GOPROXY='https://proxy.golang.org'; GOSUMDB='sum.golang.org'; GOPRIVATE=''; GONOPROXY=''; GONOSUMDB=''; GOINSECURE=''; GOAUTH='off'; GOVCS='*:off';
    GOWORK='off'; GOFLAGS=''; GOEXPERIMENT=''; GODEBUG='';
    GOPATH=(Join-Path $projectRoot '.artifacts/tools/go-build/gopath'); GOCACHE=(Join-Path $projectRoot '.artifacts/tools/go-build/cache'); GOBIN=$runRoot
}
$originalEnvironment = @{}

function Invoke-Go([string[]]$Arguments) {
    & $go @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Pinned Go build command failed; inspect its output.' }
}

function Get-VerifiedModule([string]$Module, [string]$ExpectedSum) {
    $result = Invoke-Go @('mod', 'download', '-json', $Module) | ConvertFrom-Json
    if ($result.Error -or $result.Sum -cne $ExpectedSum) { throw 'Source module checksum mismatch.' }
    return $result
}

Push-Location $projectRoot
try {
    foreach ($key in $environment.Keys) {
        $originalEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $environment[$key], 'Process')
    }
    # Nenhuma instalação global: o SDK autenticado é extraído para uma execução nova.
    $archiveRoot = Join-Path $projectRoot '.artifacts/tools/go-1.27.1'
    [void][IO.Directory]::CreateDirectory($archiveRoot)
    $archive = Join-Path $archiveRoot $pins.goArchive
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
        Invoke-WebRequest ("https://go.dev/dl/" + $pins.goArchive) -OutFile $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -cne $pins.goArchiveSha256) { throw 'Go SDK archive checksum mismatch.' }
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, (Join-Path $runRoot 'sdk'))
    $go = Join-Path $runRoot 'sdk/go/bin/go.exe'
    # GOROOT externo não pode fazer o compilador ver bibliotecas de outro SDK.
    $originalEnvironment['GOROOT'] = [Environment]::GetEnvironmentVariable('GOROOT', 'Process')
    $env:GOROOT = Join-Path $runRoot 'sdk/go'
    if ((Invoke-Go @('version')) -cne "go version $($pins.goVersion) windows/amd64") { throw 'Unexpected Go SDK version.' }
    $module = Get-VerifiedModule $pins.module $pins.moduleSum
    $source = Join-Path $runRoot 'build'
    [void][IO.Directory]::CreateDirectory($source)
    # Compila o módulo versionado sem editá-lo: a auditoria reconhece também a versão principal upstream.
    foreach ($file in @('go.mod', 'go.sum')) { Copy-Item -LiteralPath (Join-Path $pinsRoot $file) -Destination (Join-Path $source $file) }
    Push-Location $source
    try {
        Invoke-Go @('mod', 'download') | Out-Null
        Invoke-Go @('mod', 'verify') | Out-Null
        $binary = Join-Path $runRoot 'vegeta.exe'
        Invoke-Go @('build', '-mod=readonly', '-trimpath', '-buildvcs=false', '-ldflags', "-X main.Version=$($pins.version) -X main.Commit=$($pins.upstreamCommit)", '-o', $binary, $pins.module.Split('@')[0])
        foreach ($file in @('go.mod', 'go.sum')) {
            if ((Get-FileHash (Join-Path $source $file)).Hash -ne (Get-FileHash (Join-Path $pinsRoot $file)).Hash) { throw 'Build modified dependency locks.' }
        }
    } finally { Pop-Location }
    if ((Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash -cne $pins.binarySha256) { throw 'Generator build is not byte-for-byte reproducible.' }
    Get-VerifiedModule $pins.scannerModule $pins.scannerModuleSum | Out-Null
    Invoke-Go @('install', '-trimpath', $pins.scannerPackage)
    $scanner = Join-Path $runRoot 'govulncheck.exe'
    if ((Get-FileHash -LiteralPath $scanner).Hash -cne $pins.scannerSha256) { throw 'Scanner build is not byte-for-byte reproducible.' }
    $scan = Join-Path $runRoot 'vulnerabilities.txt'
    # Module-level inclui módulos presentes no binário mesmo quando seus símbolos vulneráveis não são chamados.
    & $scanner -mode=binary -scan=module -show=version -db=https://vuln.go.dev $binary > $scan 2>&1
    if ($LASTEXITCODE -ne 0 -or (Get-Content -LiteralPath $scan -Raw) -notmatch '(?m)^No vulnerabilities found\.\r?$') { throw 'Generator vulnerability scan failed or returned incomplete output.' }
    Invoke-Go @('version', '-m', $binary) | Set-Content -LiteralPath (Join-Path $runRoot 'build-info.txt') -Encoding utf8NoBOM
    Copy-Item -LiteralPath (Join-Path $module.Dir 'LICENSE') -Destination (Join-Path $runRoot 'LICENSE')
    $manifest.binary = [IO.Path]::GetRelativePath($projectRoot, $binary)
    $manifest.scanner = [IO.Path]::GetRelativePath($projectRoot, $scanner)
    $manifest.binarySha256 = $pins.binarySha256
    $manifest.scannerSha256 = (Get-FileHash -LiteralPath $scanner).Hash
    $manifest.scanSha256 = (Get-FileHash -LiteralPath $scan).Hash
    Assert-LoadGeneratorInputs $manifest.inputs $pinsRoot
    $manifest.status = 'passed'
} catch {
    $manifest.failure = $_.Exception.Message
    Write-Error $_ -ErrorAction Continue
} finally {
    foreach ($key in $originalEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $originalEnvironment[$key], 'Process') }
    Pop-Location
    $manifest.finishedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $json = $manifest | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText((Join-Path $runRoot 'manifest.json'), $json)
    # Uma falha nunca promove o candidato nem substitui a última compilação aprovada.
    if ($manifest.status -eq 'passed') { [IO.File]::WriteAllText((Join-Path $toolsRoot 'current.json'), $json) }
    Write-Host "Load generator evidence: $runRoot"
}
if ($manifest.status -ne 'passed') { exit 1 }
